using System;
using System.IO;
using System.IO.Streams;
using System.Reflection;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;

namespace Chameleon
{
	[ApiVersion(2, 0)]
	public class Chameleon : TerrariaPlugin
	{
		public const string WaitPwd4Reg = "reg-pwd";

		public override string Name => Assembly.GetExecutingAssembly().GetName().Name;

		public override string Author => "MistZZT";

		public override string Description => "账户系统交互替换方案";

		public override Version Version => Assembly.GetExecutingAssembly().GetName().Version;

		public Chameleon(Main game) : base(game) { }

		public override void Initialize()
		{
			ServerApi.Hooks.NetGetData.Register(this, OnGetData, 9999);
			ServerApi.Hooks.GamePostInitialize.Register(this, OnPostInit, 9999);
		}

		private void OnPostInit(EventArgs args)
		{
			if (!string.IsNullOrEmpty(TShock.Config.ServerPassword) || !string.IsNullOrEmpty(Netplay.ServerPassword))
			{
				TShock.Log.ConsoleError("[Chameleon] 在启用本插件的情况下, 服务器密码功能将失效.");
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
					TShockAPI.Hooks.PlayerHooks.OnPlayerPostLogin(player);
					return true;
				}

				// 如果是配置中的之前登录 part.2
				player.RequiresPassword = true;
				NetMessage.SendData((int)PacketTypes.PasswordRequired, player.Index);
				return true;
			}
			else
			{
				// 未注册 part.1
				player.SetData(WaitPwd4Reg, true);
				NetMessage.SendData((int)PacketTypes.PasswordRequired, player.Index);
				return true;
			}
		}

		private static bool HandlePassword(TSPlayer player, string password)
		{
			if (!player.RequiresPassword && !player.GetData<bool>(WaitPwd4Reg))
				return true;

			if (TShockAPI.Hooks.PlayerHooks.OnPlayerPreLogin(player, player.Name, password))
				return true;

			var user = TShock.Users.GetUserByName(player.Name);
			if (user != null/* && !TShock.Config.DisableLoginBeforeJoin*/)
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
					TShockAPI.Hooks.PlayerHooks.OnPlayerPostLogin(player);
					return true;
				}
				TShock.Utils.ForceKick(player, "账户密码错误. 若忘记, 请联系管理.", true);
				return true;
			}
			if (user == null && player.Name != TSServerPlayer.AccountName)
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
					TShock.Utils.ForceKick(player, "密码位数不能少于 " + TShock.Config.MinimumPasswordLength + " 个字符.", true);
					return true;
				}
				player.SendSuccessMessage("账户 {0} 注册成功.", user.Name);
				player.SendSuccessMessage("你的密码是 {0}.", password);
				TShock.Users.AddUser(user);
				TShock.Log.ConsoleInfo("玩家 {0} 注册了新账户: {1}.", player.Name, user.Name);

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
				TShockAPI.Hooks.PlayerHooks.OnPlayerPostLogin(player);
				return true;
			}

			TShock.Utils.ForceKick(player, "数次错误密码尝试.", true);
			return true;
		}
	}
}
