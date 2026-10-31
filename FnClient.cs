using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FnShare
{
    // 服务器返回失败（result != succ）或连接/协议异常
    public class FnApiException : Exception
    {
        public FnApiException(string message) : base(message)
        {
        }
    }

    // 飞牛 fnOS 外链分享 WebSocket 客户端。
    // 协议移植自 POC 验证脚本，原样复刻以下规则：
    //  - 连接 {base}/websocket?type=main
    //  - util.crypto.getRSAPub（明文）取 pub + si
    //  - user.login 整条 AES-256-CBC 加密，AES 密钥用 RSA PKCS#1 v1.5 包裹；
    //    登录响应 secret 用同一 AES 密钥解出，作 HMAC-SHA256 密钥
    //  - 之后每条消息 = base64(HMAC-SHA256(secret, json)) + json
    //  - reqid = {Unix秒:08x}{backId}{counter:04x}，backId 随响应更新
    //  - result=doing 时继续等同一 reqid 的最终响应
    //  - JSON 序列化与 JS JSON.stringify 一致：无空格、不转义中文
    //  - Cookie 不是必需的，本客户端不做 HTTP 预取
    public sealed class FnClient : IDisposable
    {
        private readonly ClientWebSocket _ws = new ClientWebSocket();
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        private string _backId = "0000000000000000";
        private int _counter;
        private byte[] _aesKey = new byte[0];
        private byte[] _iv = new byte[0];
        private byte[] _hmacKey;

        private FnClient()
        {
        }

        // 建立连接并完成握手＋登录；任一步失败即释放并抛异常
        public static async Task<FnClient> ConnectAsync(AppConfig cfg, string password, CancellationToken ct = default)
        {
            var c = new FnClient();
            try
            {
                await c.ConnectInternalAsync(cfg, password, ct).ConfigureAwait(false);
                return c;
            }
            catch
            {
                c.Dispose();
                throw;
            }
        }

        private async Task ConnectInternalAsync(AppConfig cfg, string password, CancellationToken ct)
        {
            string baseUrl = (cfg.InternalBase ?? "").Trim().TrimEnd('/');
            if (string.IsNullOrEmpty(baseUrl)) throw new FnApiException("未配置 NAS 内网地址。");
            if (string.IsNullOrEmpty(cfg.Username)) throw new FnApiException("未配置 NAS 用户名。");
            if (string.IsNullOrEmpty(password)) throw new FnApiException("未提供 NAS 账号密码。");

            Uri wsUri = BuildWsUri(baseUrl);
            _ws.Options.SetRequestHeader("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36");
            _ws.Options.SetRequestHeader("Origin", baseUrl);
            if (cfg.IgnoreTlsErrors && wsUri.Scheme == "wss")
                _ws.Options.RemoteCertificateValidationCallback = (s, cert, chain, errors) => true;

            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                cts.CancelAfter(TimeSpan.FromSeconds(20));
                await _ws.ConnectAsync(wsUri, cts.Token).ConfigureAwait(false);
            }

            // 1. 明文取 RSA 公钥与 si
            string pub;
            string si;
            using (JsonDocument pubDoc = await CallPlainAsync("util.crypto.getRSAPub", null, ct).ConfigureAwait(false))
            {
                JsonElement root = pubDoc.RootElement;
                pub = root.TryGetProperty("pub", out JsonElement pubEl) ? pubEl.GetString() ?? "" : "";
                si = root.TryGetProperty("si", out JsonElement siEl) ? siEl.GetString() ?? "" : "";
            }
            if (string.IsNullOrEmpty(pub))
                throw new FnApiException("获取 RSA 公钥失败：响应中没有 pub 字段。");

            // 2. 生成 AES 密钥与 IV（与 POC 一致：32 位随机字母数字作密钥）
            _aesKey = Encoding.UTF8.GetBytes(RandomAlnum(32));
            _iv = new byte[16];
            RandomNumberGenerator.Fill(_iv);

            // 3. 登录：整条消息 AES 加密，AES 密钥 RSA 包裹
            string rid = NewReqId();
            var loginFields = new Dictionary<string, object>
            {
                ["reqid"] = rid,
                ["req"] = "user.login",
                ["user"] = cfg.Username,
                ["password"] = password,
                ["deviceType"] = "Browser",
                ["deviceName"] = "Windows-Google Chrome",
                ["stay"] = false,
                ["did"] = Guid.NewGuid().ToString("N"),
                ["si"] = si,
            };
            string innerJson = Serialize(loginFields);
            string aesB64 = AesEncrypt(innerJson, _aesKey, _iv);
            string rsaB64;
            using (RSA rsa = RSA.Create())
            {
                rsa.ImportFromPem(NormalizePem(pub));
                rsaB64 = Convert.ToBase64String(rsa.Encrypt(_aesKey, RSAEncryptionPadding.Pkcs1));
            }
            var outer = new Dictionary<string, object>
            {
                ["req"] = "encrypted",
                ["iv"] = Convert.ToBase64String(_iv),
                ["rsa"] = rsaB64,
                ["aes"] = aesB64,
            };
            using (JsonDocument loginDoc = await SendAndWaitAsync(Serialize(outer), rid, ct).ConfigureAwait(false))
            {
                string secretB64 = loginDoc.RootElement.TryGetProperty("secret", out JsonElement secEl)
                    ? secEl.GetString() ?? "" : "";
                if (string.IsNullOrEmpty(secretB64))
                    throw new FnApiException("登录响应没有 secret 字段，字段可能与预期不同。");
                _hmacKey = AesDecrypt(Convert.FromBase64String(secretB64), _aesKey, _iv);
            }
            if (_hmacKey == null || _hmacKey.Length == 0)
                throw new FnApiException("解密登录 secret 失败。");
        }

        // ---- 对外接口（与 POC 一一对应） ----

        public async Task<string> GetDefaultLinkAsync(CancellationToken ct = default)
        {
            using (JsonDocument doc = await CallSignedAsync("appcgi.sharesvr.share.link.default", null, ct).ConfigureAwait(false))
            {
                JsonElement data = doc.RootElement.GetProperty("data");
                return data.TryGetProperty("defaultLink", out JsonElement dl) ? dl.GetString() ?? "" : "";
            }
        }

        public async Task<ShareRecord> CreateShareAsync(
            string name, string nasPath, int maxAccess, long? validToSeconds, string passwd,
            CancellationToken ct = default)
        {
            var data = new Dictionary<string, object>
            {
                ["name"] = name,
                ["files"] = new List<string> { nasPath },
                ["maxAccess"] = maxAccess,
            };
            if (validToSeconds.HasValue) data["validTo"] = validToSeconds.Value;
            if (!string.IsNullOrEmpty(passwd)) data["passwd"] = passwd;
            using (JsonDocument doc = await CallSignedAsync("appcgi.sharesvr.share.link.create", data, ct).ConfigureAwait(false))
            {
                return JsonSerializer.Deserialize<ShareRecord>(
                    doc.RootElement.GetProperty("data").GetRawText(), _jsonOptions);
            }
        }

        public async Task<ShareRecord> GetShareAsync(string id, CancellationToken ct = default)
        {
            var data = new Dictionary<string, object> { ["id"] = id };
            using (JsonDocument doc = await CallSignedAsync("appcgi.sharesvr.share.link.get", data, ct).ConfigureAwait(false))
            {
                return JsonSerializer.Deserialize<ShareRecord>(
                    doc.RootElement.GetProperty("data").GetRawText(), _jsonOptions);
            }
        }

        public async Task<Tuple<int, List<ShareRecord>>> ListSharesAsync(CancellationToken ct = default)
        {
            using (JsonDocument doc = await CallSignedAsync(
                "appcgi.sharesvr.share.link.list", new Dictionary<string, object>(), ct).ConfigureAwait(false))
            {
                JsonElement data = doc.RootElement.GetProperty("data");
                int total = data.TryGetProperty("total", out JsonElement t) ? t.GetInt32() : 0;
                var list = new List<ShareRecord>();
                if (data.TryGetProperty("list", out JsonElement arr) && arr.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement el in arr.EnumerateArray())
                    {
                        ShareRecord rec = JsonSerializer.Deserialize<ShareRecord>(el.GetRawText(), _jsonOptions);
                        if (rec != null) list.Add(rec);
                    }
                }
                return Tuple.Create(total, list);
            }
        }

        // ---- 收发层 ----

        private async Task<JsonDocument> CallPlainAsync(string req, Dictionary<string, object> fields, CancellationToken ct)
        {
            string rid = NewReqId();
            var msg = new Dictionary<string, object> { ["reqid"] = rid, ["req"] = req };
            if (fields != null)
            {
                foreach (KeyValuePair<string, object> kv in fields) msg[kv.Key] = kv.Value;
            }
            return await SendAndWaitAsync(Serialize(msg), rid, ct).ConfigureAwait(false);
        }

        private async Task<JsonDocument> CallSignedAsync(string req, Dictionary<string, object> data, CancellationToken ct)
        {
            string rid = NewReqId();
            var msg = new Dictionary<string, object> { ["reqid"] = rid, ["req"] = req };
            if (data != null) msg["data"] = data;
            string json = Serialize(msg);
            string sig;
            using (var h = new HMACSHA256(_hmacKey))
            {
                sig = Convert.ToBase64String(h.ComputeHash(Encoding.UTF8.GetBytes(json)));
            }
            return await SendAndWaitAsync(sig + json, rid, ct).ConfigureAwait(false);
        }

        private async Task<JsonDocument> SendAndWaitAsync(string frame, string expectReqId, CancellationToken ct)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(frame);
            await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct).ConfigureAwait(false);
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(20));
                while (true)
                {
                    string text = await ReceiveTextAsync(timeout.Token).ConfigureAwait(false);
                    JsonDocument doc;
                    try
                    {
                        doc = JsonDocument.Parse(text);
                    }
                    catch
                    {
                        continue; // 非 JSON 帧，跳过
                    }
                    JsonElement root = doc.RootElement;
                    if (root.TryGetProperty("res", out JsonElement resEl) && resEl.GetString() == "pong")
                    {
                        doc.Dispose();
                        continue;
                    }
                    if (root.TryGetProperty("backId", out JsonElement backEl)
                        && backEl.ValueKind == JsonValueKind.String)
                    {
                        _backId = backEl.GetString();
                    }
                    string respReqId = root.TryGetProperty("reqid", out JsonElement ridEl)
                        ? ridEl.GetString() ?? "" : "";
                    if (!string.Equals(respReqId, expectReqId, StringComparison.Ordinal))
                    {
                        doc.Dispose();
                        continue; // 通知类消息
                    }
                    string result = root.TryGetProperty("result", out JsonElement rEl)
                        ? rEl.GetString() ?? "" : "";
                    if (result == "doing")
                    {
                        doc.Dispose();
                        continue; // 继续等同一 reqid 的最终响应
                    }
                    if (result != "succ")
                    {
                        string detail = DescribeFailure(root);
                        doc.Dispose();
                        throw new FnApiException(detail);
                    }
                    return doc;
                }
            }
        }

        private async Task<string> ReceiveTextAsync(CancellationToken ct)
        {
            using (var ms = new MemoryStream())
            {
                var buffer = new byte[8192];
                while (true)
                {
                    WebSocketReceiveResult r = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct).ConfigureAwait(false);
                    if (r.MessageType == WebSocketMessageType.Close)
                        throw new FnApiException("服务器关闭了 WebSocket 连接。");
                    ms.Write(buffer, 0, r.Count);
                    if (r.EndOfMessage) break;
                }
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        // ---- 工具 ----

        private string NewReqId()
        {
            _counter++;
            long unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return string.Format("{0:x8}{1}{2:x4}", unix, _backId, _counter);
        }

        private string Serialize(object obj)
        {
            return JsonSerializer.Serialize(obj, _jsonOptions);
        }

        private static Uri BuildWsUri(string baseUrl)
        {
            var ub = new UriBuilder(baseUrl);
            ub.Scheme = ub.Scheme == "https" ? "wss" : "ws";
            ub.Path = "/websocket";
            ub.Query = "type=main";
            return ub.Uri;
        }

        private static string NormalizePem(string pub)
        {
            string p = (pub ?? "").Trim();
            if (p.Contains("BEGIN")) return p;
            return "-----BEGIN PUBLIC KEY-----\n" + p + "\n-----END PUBLIC KEY-----\n";
        }

        private static string RandomAlnum(int length)
        {
            const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var sb = new StringBuilder(length);
            byte[] buf = new byte[length];
            RandomNumberGenerator.Fill(buf);
            for (int i = 0; i < length; i++) sb.Append(chars[buf[i] % chars.Length]);
            return sb.ToString();
        }

        private static string AesEncrypt(string plain, byte[] key, byte[] iv)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                using (ICryptoTransform encryptor = aes.CreateEncryptor())
                {
                    byte[] data = Encoding.UTF8.GetBytes(plain);
                    return Convert.ToBase64String(encryptor.TransformFinalBlock(data, 0, data.Length));
                }
            }
        }

        private static byte[] AesDecrypt(byte[] cipher, byte[] key, byte[] iv)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                using (ICryptoTransform decryptor = aes.CreateDecryptor())
                {
                    return decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
                }
            }
        }

        // 失败原因只取结构化字段，不全文转储响应（响应可能含敏感字段）
        private static string DescribeFailure(JsonElement root)
        {
            string result = root.TryGetProperty("result", out JsonElement r) ? r.GetString() ?? "" : "";
            string errno = root.TryGetProperty("errno", out JsonElement e) ? e.ToString() : "";
            string errmsg = "";
            if (root.TryGetProperty("errmsg", out JsonElement m)) errmsg = m.GetString() ?? "";
            if (root.TryGetProperty("error", out JsonElement er)) errmsg = er.GetString() ?? errmsg;
            var sb = new StringBuilder("服务器返回失败");
            if (!string.IsNullOrEmpty(result)) sb.Append("（result=").Append(result).Append("）");
            if (!string.IsNullOrEmpty(errno)) sb.Append("，errno=").Append(errno);
            if (!string.IsNullOrEmpty(errmsg)) sb.Append("：").Append(errmsg);
            return sb.ToString();
        }

        public void Dispose()
        {
            try
            {
                _ws.Abort();
            }
            catch
            {
            }
            _ws.Dispose();
        }
    }
}
