﻿using System;
using System.IO;
using System.IO.Streams;
using System.Linq;
using System.Reflection;
using OTAPI;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;
using TShockAPI.Hooks;

namespace Chameleon
{
	[ApiVersion(2, 1)]
	public class Chameleon : TerrariaPlugin
	{
		public const string WaitPwd4Reg = "reg-pwd";

		public const ushort Size = 10;

		internal static Configuration Config;

		public static string[] PrepareList = new string[Size];

		public override string Name => Assembly.GetExecutingAssembly().GetName().Name;

		public override string Author => "MistZZT";

		public override string Description => "账户系统交互替换方案";

		public override Version Version => Assembly.GetExecutingAssembly().GetName().Version;

		private readonly string _clientWasBooted;

		public Chameleon(Main game) : base(game)
		{
			_clientWasBooted = Terraria.Localization.Language.GetTextValue("CLI.ClientWasBooted", "", "").Trim();
		}

		public override void Initialize()
		{
			Hooks.Console.WriteLine += OnWriteLine;

			ServerApi.Hooks.NetGetData.Register(this, OnGetData, 9999);
			ServerApi.Hooks.GamePostInitialize.Register(this, OnPostInit, 9999);
			ServerApi.Hooks.GameInitialize.Register(this, OnInit);

			GeneralHooks.ReloadEvent += ReloadConfig;
		}

		private HookResult OnWriteLine(ConsoleHookArgs value)
		{
			return value.Arg1?.ToString().Contains(_clientWasBooted) == true ? HookResult.Cancel : HookResult.Continue;
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				Hooks.Console.WriteLine -= OnWriteLine;

				ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
				ServerApi.Hooks.GamePostInitialize.Deregister(this, OnPostInit);
				ServerApi.Hooks.GameInitialize.Deregister(this, OnInit);

				GeneralHooks.ReloadEvent -= ReloadConfig;
			}
			base.Dispose(disposing);
		}

		private static void OnInit(EventArgs args)
		{
			LoadConfig();
		}

		private static void OnPostInit(EventArgs args)
		{
			if (!string.IsNullOrEmpty(TShock.Config.ServerPassword) || !string.IsNullOrEmpty(Netplay.ServerPassword))
			{
				TShock.Log.ConsoleError("[Chameleon] 在启用本插件的情况下, 服务器密码功能将失效.");
			}

			if (TShock.Config.DisableLoginBeforeJoin)
			{
				TShock.Log.ConsoleError("[Chameleon] 在启用本插件的情况下, 入服前登录将被强制开启.");
				TShock.Config.DisableLoginBeforeJoin = true;
			}

			if (!TShock.Config.RequireLogin && !TShock.ServerSideCharacterConfig.Enabled)
			{
				TShock.Log.ConsoleError("[Chameleon] 在启用本插件的情况下, 注册登录将被强制开启.");
				TShock.Config.RequireLogin = true;
			}
		}

		private static void OnGetData(GetDataEventArgs args)
		{
			if (args.Handled)
				return;

			var type = args.MsgID;

			var player = TShock.Players[args.Msg.whoAmI];
			if (player == null || !player.ConnectionAlive)
			{
				args.Handled = true;
				return;
			}

			if (player.RequiresPassword && type != PacketTypes.PasswordSend)
			{
				args.Handled = true;
				return;
			}

			if ((player.State < 10 || player.Dead) && (int)type > 12 && (int)type != 16 && (int)type != 42 && (int)type != 50 &&
				(int)type != 38 && (int)type != 21 && (int)type != 22)
			{
				args.Handled = true;
				return;
			}

			using (var data = new MemoryStream(args.Msg.readBuffer, args.Index, args.Length - 1))
			{
				// ReSharper disable once ConvertIfStatementToSwitchStatement
				if (type == PacketTypes.ContinueConnecting2)
				{
					args.Handled = HandleConnecting(player);
				}
				else if (type == PacketTypes.PasswordSend)
				{
					args.Handled = HandlePassword(player, data.ReadString());
				}
			}
		}

		private static bool HandleConnecting(TSPlayer player)
		{
			var user = TShock.Users.GetUserByName(player.Name);
			player.DataWhenJoined = new PlayerData(player);
			player.DataWhenJoined.CopyCharacter(player);

			if (user != null)
			{
				// uuid自动登录 已注册part.2
				if (!TShock.Config.DisableUUIDLogin && user.UUID == player.UUID)
				{
					if (player.State == 1)
						player.State = 2;
					NetMessage.SendData((int)PacketTypes.WorldInfo, player.Index);

					player.PlayerData = TShock.CharacterDB.GetPlayerData(player, user.ID);

					var group = TShock.Utils.GetGroup(user.Group);

					player.Group = group;
					player.tempGroup = null;
					player.User = user;
					player.IsLoggedIn = true;
					player.IgnoreActionsForInventory = "none";

					if (Main.ServerSideCharacter)
					{
						if (player.HasPermission(Permissions.bypassssc))
						{
							player.PlayerData.CopyCharacter(player);
							TShock.CharacterDB.InsertPlayerData(player);
						}
						player.PlayerData.RestoreCharacter(player);
					}
					player.LoginFailsBySsi = false;

					if (player.HasPermission(Permissions.ignorestackhackdetection))
						player.IgnoreActionsForCheating = "none";

					if (player.HasPermission(Permissions.usebanneditem))
						player.IgnoreActionsForDisabledArmor = "none";

					player.SendSuccessMessage($"已经验证 {user.Name} 登录完毕.");
					TShock.Log.ConsoleInfo(player.Name + " 成功验证登录.");
					PlayerHooks.OnPlayerPostLogin(player);
					return true;
				}

				// 使用密码登录 part.2
				player.RequiresPassword = true;
				NetMessage.SendData((int)PacketTypes.PasswordRequired, player.Index);
				return true;
			}

			if (Config.EnableForcedHint && !PrepareList.Contains(player.Name))
			{
				AddToList(player.Name);
				Kick(player, string.Join("\n", Config.Hints), Config.Greeting);
				return true;
			}

			// 未注册 part.1
			player.SetData(WaitPwd4Reg, true);
			NetMessage.SendData((int)PacketTypes.PasswordRequired, player.Index);
			return true;
		}

		private static bool HandlePassword(TSPlayer player, string password)
		{
			var isRegister = player.GetData<bool>(WaitPwd4Reg);

			if (!player.RequiresPassword && !isRegister)
				return true;

			if (!isRegister && PlayerHooks.OnPlayerPreLogin(player, player.Name, password))
				return true;

			var user = TShock.Users.GetUserByName(player.Name);
			if (user != null)
			{
				if (user.VerifyPassword(password))
				{
					player.RequiresPassword = false;
					player.PlayerData = TShock.CharacterDB.GetPlayerData(player, user.ID);

					if (player.State == 1)
						player.State = 2;
					NetMessage.SendData((int)PacketTypes.WorldInfo, player.Index);

					var group = TShock.Utils.GetGroup(user.Group);

					player.Group = group;
					player.tempGroup = null;
					player.User = user;
					player.IsLoggedIn = true;
					player.IgnoreActionsForInventory = "none";

					if (Main.ServerSideCharacter)
					{
						if (player.HasPermission(Permissions.bypassssc))
						{
							player.PlayerData.CopyCharacter(player);
							TShock.CharacterDB.InsertPlayerData(player);
						}
						player.PlayerData.RestoreCharacter(player);
					}
					player.LoginFailsBySsi = false;

					if (player.HasPermission(Permissions.ignorestackhackdetection))
						player.IgnoreActionsForCheating = "none";

					if (player.HasPermission(Permissions.usebanneditem))
						player.IgnoreActionsForDisabledArmor = "none";


					player.SendSuccessMessage($"已经验证 {user.Name} 登录完毕.");
					TShock.Log.ConsoleInfo(player.Name + " 成功验证登录.");
					TShock.Users.SetUserUUID(user, player.UUID);
					PlayerHooks.OnPlayerPostLogin(player);
					return true;
				}
				Kick(player, Config.VerficationFailedMessage, "验证失败");
				return true;
			}
			if (player.Name != TSServerPlayer.AccountName)
			{
				user = new User
				{
					Name = player.Name,
					Group = TShock.Config.DefaultRegistrationGroupName,
					UUID = player.UUID
				};
				try
				{
					user.CreateBCryptHash(password);
				}
				catch (ArgumentOutOfRangeException)
				{
					Kick(player, "密码位数不能少于 " + TShock.Config.MinimumPasswordLength + " 个字符.", "注册失败");
					return true;
				}
				player.SendSuccessMessage("账户 {0} 注册成功.", user.Name);
				player.SendSuccessMessage("你的密码是 {0}.", password);
				TShock.Users.AddUser(user);
				TShock.Log.ConsoleInfo("玩家 {0} 注册了新账户: {1}.", player.Name, user.Name);

				player.RequiresPassword = false;
				player.SetData(WaitPwd4Reg, false);
				player.PlayerData = TShock.CharacterDB.GetPlayerData(player, user.ID);

				if (player.State == 1)
					player.State = 2;
				NetMessage.SendData((int)PacketTypes.WorldInfo, player.Index);

				var group = TShock.Utils.GetGroup(user.Group);

				player.Group = group;
				player.tempGroup = null;
				player.User = user;
				player.IsLoggedIn = true;
				player.IgnoreActionsForInventory = "none";

				if (Main.ServerSideCharacter)
				{
					if (player.HasPermission(Permissions.bypassssc))
					{
						player.PlayerData.CopyCharacter(player);
						TShock.CharacterDB.InsertPlayerData(player);
					}
					player.PlayerData.RestoreCharacter(player);
				}
				player.LoginFailsBySsi = false;

				if (player.HasPermission(Permissions.ignorestackhackdetection))
					player.IgnoreActionsForCheating = "none";

				if (player.HasPermission(Permissions.usebanneditem))
					player.IgnoreActionsForDisabledArmor = "none";


				player.SendSuccessMessage($"已经验证 {user.Name} 登录完毕.");
				TShock.Log.ConsoleInfo(player.Name + " 成功验证登录.");
				TShock.Users.SetUserUUID(user, player.UUID);
				PlayerHooks.OnPlayerPostLogin(player);
				return true;
			}

			// 系统预留账户名
			Kick(player, "该用户名已被占用.", "请更换人物名");
			return true;
		}

		private static void AddToList(string playerName)
		{
			var index = 0;
			while (index < PrepareList.Length && !string.IsNullOrEmpty(PrepareList[index])) index++;
			PrepareList[index % PrepareList.Length] = playerName;
		}

		public static void Kick(TSPlayer player, string msg, string custom)
		{
			if (!player.ConnectionAlive)
				return;

			player.SilentKickInProgress = true;
			player.Disconnect($"{custom}: {msg}");
			TShock.Log.ConsoleInfo($"向{player.Name}发送通知完毕.");
		}

		private static void LoadConfig()
		{
			Config = Configuration.Read(Configuration.FilePath);
			Config.Write(Configuration.FilePath);

			if (Config.AwaitBufferSize != Size)
			{
				Array.Resize(ref PrepareList, Config.AwaitBufferSize);
				Array.Clear(PrepareList, 0, Config.AwaitBufferSize);
			}
		}

		private static void ReloadConfig(ReloadEventArgs args)
		{
			LoadConfig();

			args.Player?.SendSuccessMessage("重新加载 {0} 配置完毕.", typeof(Chameleon).Name);
		}
	}
}
