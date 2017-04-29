﻿using System.IO;
using Newtonsoft.Json;
using TShockAPI;

namespace Chameleon
{
	[JsonObject(MemberSerialization.OptIn)]
	internal class Configuration
	{
		public static readonly string FilePath = Path.Combine(TShock.SavePath, "Chameleon.json");

		[JsonProperty("等待列表长度")]
		public ushort AwaitBufferSize = Chameleon.Size;

		[JsonProperty("启用强制提示显示")]
		public bool EnableForcedHint = true;

		[JsonProperty("强制提示欢迎语")]
		public string Greeting ="   欢迎来到Terraria Boss服务器";

		[JsonProperty("验证失败提示语")]
		public string VerficationFailedMessage = "         账户密码错误。\r\n\r\n         若你第一次进服，请换一个人物名；\r\n         若忘记密码，请联系管理。";

		[JsonProperty("强制提示文本")]
		public string[] Hints =
		{
			" ↓↓ 请看下面的提示以进服 ↓↓",
			" \r\n         看完下面的再点哦→",
			" 1. 请确保你已经阅读进服教程 http://tr.xcoder.cc/ ",
			" 2. 请再次加服 \r\n 3. 在\"服务器密码\"中输入自己的密码, 以后加服时输入这个密码即可."
		};

		public void Write(string path)
		{
			using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Write))
			{
				var str = JsonConvert.SerializeObject(this, Formatting.Indented);
				using (var sw = new StreamWriter(fs))
				{
					sw.Write(str);
				}
			}
		}

		public static Configuration Read(string path)
		{
			if (!File.Exists(path))
				return new Configuration();
			using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				using (var sr = new StreamReader(fs))
				{
					var cf = JsonConvert.DeserializeObject<Configuration>(sr.ReadToEnd());
					return cf;
				}
			}
		}
	}
}
