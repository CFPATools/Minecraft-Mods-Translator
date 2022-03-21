using System;
using System.Text;
using System.Net;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Collections;

namespace TransAPICSharpDemo
{
    class TransAPI
    {
        public static Hashtable hashtable = new Hashtable();

        // Hash表和翻译作判断
        public static string Contect(string q)
        {
            if (hashtable.Contains(q))
            {
                return hashtable[q].ToString();
            }
            else
            {
                var trans = Translate(q);
                hashtable.Add(q, trans);
                return trans;
            }
        }
        // 翻译请求主体
        public static string Translate(string q)
        {
            // 原文

            // 源语言
            string from = "en";
            // 目标语言
            string to = "zh";
            // 改成您的APP ID
            string appId = "";
            Random rd = new Random();
            string salt = rd.Next(100000).ToString();
            // 改成您的密钥
            string secretKey = "";
            string sign = EncryptString(appId + q + salt + secretKey);
            string url = "http://api.fanyi.baidu.com/api/trans/vip/translate?";
            url += "q=" + WebUtility.UrlEncode(q);
            url += "&from=" + from;
            url += "&to=" + to;
            url += "&appid=" + appId;
            url += "&salt=" + salt;
            url += "&sign=" + sign;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.ContentType = "text/html;charset=UTF-8";
            request.UserAgent = null;
            request.Timeout = 6000;
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            Stream myResponseStream = response.GetResponseStream();
            StreamReader myStreamReader = new StreamReader(myResponseStream, Encoding.GetEncoding("utf-8"));
            string retString = myStreamReader.ReadToEnd();
            myStreamReader.Close();
            myResponseStream.Close();

            var json = JsonConvert.DeserializeObject<Root>(retString);
            return Unicode2String(json.trans_result[0].dst);
        }
        // 计算MD5值
        public static string EncryptString(string str)
        {
            MD5 md5 = MD5.Create();
            // 将字符串转换成字节数组
            byte[] byteOld = Encoding.UTF8.GetBytes(str);
            // 调用加密方法
            byte[] byteNew = md5.ComputeHash(byteOld);
            // 将加密结果转换为字符串
            StringBuilder sb = new StringBuilder();
            foreach (byte b in byteNew)
            {
                // 将字节转换成16进制表示的字符串，
                sb.Append(b.ToString("x2"));
            }
            // 返回加密的字符串
            return sb.ToString();
        }
        // Unicode转字符串
        public static string Unicode2String(string source)
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
