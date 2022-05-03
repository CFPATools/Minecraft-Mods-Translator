using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Translator
{
    internal static class TransApi
    {
        private static Hashtable hashtable = new Hashtable();

        // Hash表和翻译作判断
        public static Task<string> Contect(string q)
        {
            if (q == "")
                return Task.FromResult("");

            if (hashtable.Contains(q))
                return Task.FromResult(hashtable[q].ToString());

            var trans = Translate(q);
            hashtable.Add(q, trans);
            return Task.FromResult(trans);
        }
        // 翻译请求主体
        private static string Translate(string q)
        {
            // 原文

            // 源语言
            const string @from = "en";
            // 目标语言
            const string to = "";
            // 改成您的APP ID
            const string appId = "";
            Random rd = new();
            var salt = rd.Next(100000).ToString();
            // 改成您的密钥
            const string secretKey = "qxKLQlrQTjWV23GCWb9A";
            var sign = EncryptString(appId + q + salt + secretKey);
            var url = "http://api.fanyi.baidu.com/api/trans/vip/translate?";
            url += "q=" + WebUtility.UrlEncode(q);
            url += "&from=" + from;
            url += "&to=" + to;
            url += "&appid=" + appId;
            url += "&salt=" + salt;
            url += "&sign=" + sign;
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.ContentType = "text/html;charset=UTF-8";
            request.UserAgent = null;
            request.Timeout = 6000;
            var response = (HttpWebResponse)request.GetResponse();
            var myResponseStream = response.GetResponseStream();
            var myStreamReader = new StreamReader(myResponseStream, Encoding.GetEncoding("utf-8"));
            var retString = myStreamReader.ReadToEnd();
            myStreamReader.Close();
            myResponseStream.Close();

            var json = JsonConvert.DeserializeObject<Root>(retString);
            return Unicode2String(json.trans_result[0].dst);
        }
        // 计算MD5值
        private static string EncryptString(string str)
        {
            var md5 = MD5.Create();
            // 将字符串转换成字节数组
            var byteOld = Encoding.UTF8.GetBytes(str);
            // 调用加密方法
            var byteNew = md5.ComputeHash(byteOld);
            // 将加密结果转换为字符串
            var sb = new StringBuilder();
            foreach (byte b in byteNew)
            {
                // 将字节转换成16进制表示的字符串，
                sb.Append(b.ToString("x2"));
            }
            // 返回加密的字符串
            return sb.ToString();
        }
        // Unicode转字符串
        private static string Unicode2String(string source)
        {
            return new Regex(@"\\u([0-9A-F]{4})", RegexOptions.IgnoreCase | RegexOptions.Compiled).Replace(
                         source, x => string.Empty + Convert.ToChar(Convert.ToUInt16(x.Result("$1"), 16)));
        }
    }

    public class Trans_resultItem
    {
        /// <summary>
        /// 
        /// </summary>
        public string src { get; set; }
        /// <summary>
        /// 用板条箱包装物品。
        /// </summary>
        public string dst { get; set; }
    }

    public class Root
    {
        /// <summary>
        /// 
        /// </summary>
        public string @from { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string to { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public List<Trans_resultItem> trans_result { get; set; }
    }

}
