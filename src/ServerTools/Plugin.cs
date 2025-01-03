using Microsoft.Xna.Framework;
using MonoMod.RuntimeDetour;
using Newtonsoft.Json;
using Rests;
using System.Text.RegularExpressions;
using Terraria;
using Terraria.GameContent.Creative;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;

namespace ServerTools;

[ApiVersion(2, 1)]
public partial class Plugin : TerrariaPlugin
{
    public override string Author => "少司命";// 插件作者

    public override string Description => "服务器工具";// 插件说明

    public override string Name => "ServerTools";// 插件名字

    public override Version Version => new(1, 1, 7, 8);// 插件版本

    private static Config Config = new();

    private readonly string PATH = Path.Combine(TShock.SavePath, "ServerTools.json");

    private DateTime LastCommandUseTime = DateTime.Now;

    private static long TimerCount = 0;

    private readonly Dictionary<string, DateTime> PlayerDeath = new();

    public event Action<EventArgs>? Timer;

    public static Hook CmdHook = null!;

    public static Hook AccountInfoHook = null!;

    public Plugin(Main game) : base(game)
    {
        this._reloadHandler = (_) => this.LoadConfig();
    }
    private readonly GeneralHooks.ReloadEventD _reloadHandler;
    private RestCommand[] addRestCommands = null!;
    public override void Initialize()
    {

        this.LoadConfig();
        #region 钩子
        ServerApi.Hooks.GamePostInitialize.Register(this, this.PostInitialize);
        ServerApi.Hooks.ServerJoin.Register(this, this.OnJoin);
        ServerApi.Hooks.GameInitialize.Register(this, this.OnInitialize);
        ServerApi.Hooks.NetGreetPlayer.Register(this, this.OnGreetPlayer);
        ServerApi.Hooks.ServerLeave.Register(this, this.OnLeave);
        ServerApi.Hooks.GameUpdate.Register(this, this.OnUpdate);
        ServerApi.Hooks.NetGetData.Register(this, this.GetData);
        ServerApi.Hooks.NetGreetPlayer.Register(this, this.OnGreet);
        ServerApi.Hooks.ServerLeave.Register(this, this._OnLeave);
        ServerApi.Hooks.NpcStrike.Register(this, OnStrike);
        ServerApi.Hooks.NpcAIUpdate.Register(this, OnNPCUpdate);
        #endregion

        #region 指令
        Commands.ChatCommands.Add(new Command(Permissions.clear, this.Clear, "clp"));
        Commands.ChatCommands.Add(new Command("servertool.query.exit", this.Exit, "退出", "toolexit"));
        Commands.ChatCommands.Add(new Command("servertool.query.wall", this.WallQ, "查花苞", "scp"));
        Commands.ChatCommands.Add(new Command("servertool.query.wall", this.RWall, "移除花苞", "rcp"));
        Commands.ChatCommands.Add(new Command("servertool.user.kick", this.SelfKick, "自踢", "selfkick"));
        Commands.ChatCommands.Add(new Command("servertool.user.kill", this.SelfKill, "自杀", "selfkill"));
        Commands.ChatCommands.Add(new Command("servertool.user.ghost", this.Ghost, "ghost"));
        Commands.ChatCommands.Add(new Command("servertool.set.journey", this.JourneyDiff, "旅途难度", "journeydiff"));
        Commands.ChatCommands.Add(new Command("servertool.user.dead", this.DeathRank, "死亡排行", "deadrank"));
        Commands.ChatCommands.Add(new Command("servertool.user.online", this.OnlineRank, "在线排行", "onlinerank"));
        #endregion
        #region TShcok 钩子
        GetDataHandlers.NewProjectile.Register(this.NewProj);
        GetDataHandlers.ItemDrop.Register(this.OnItemDrop);
        GetDataHandlers.KillMe.Register(this.KillMe);
        GetDataHandlers.PlayerSpawn.Register(this.OnPlayerSpawn);
        GetDataHandlers.PlayerUpdate.Register(this.OnUpdate);
        GeneralHooks.ReloadEvent += this._reloadHandler;
        #endregion
        CmdHook = new Hook(typeof(Commands).GetMethod(nameof(Commands.HandleCommand)), CommandHook);
        AccountInfoHook = new Hook(typeof(Commands).GetMethod("ViewAccountInfo", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static), ViewAccountInfo);
        #region RestAPI
        this.addRestCommands = new RestCommand[]
        {
    new RestCommand("/deathrank", this.DeadRank),
    new RestCommand("/onlineDuration", this.Queryduration)
        };
        foreach (var command in this.addRestCommands)
        {
            TShock.RestApi.Register(command);
        }
        #endregion
        Timer += this.OnUpdatePlayerOnline;
        On.OTAPI.Hooks.MessageBuffer.InvokeGetData += this.MessageBuffer_InvokeGetData;
        this.HandleCommandLine(Environment.GetCommandLineArgs());
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            #region 钩子
            GeneralHooks.ReloadEvent -= this._reloadHandler;
            ServerApi.Hooks.GamePostInitialize.Deregister(this, this.PostInitialize);
            ServerApi.Hooks.ServerJoin.Deregister(this, this.OnJoin);
            ServerApi.Hooks.GameInitialize.Deregister(this, this.OnInitialize);
            ServerApi.Hooks.NetGreetPlayer.Deregister(this, this.OnGreetPlayer);
            ServerApi.Hooks.ServerLeave.Deregister(this, this.OnLeave);
            ServerApi.Hooks.GameUpdate.Deregister(this, this.OnUpdate);
            ServerApi.Hooks.NetGetData.Deregister(this, this.GetData);
            ServerApi.Hooks.NetGreetPlayer.Deregister(this, this.OnGreet);
            ServerApi.Hooks.ServerLeave.Deregister(this, this._OnLeave);
            ServerApi.Hooks.NpcStrike.Deregister(this, OnStrike);
            ServerApi.Hooks.NpcAIUpdate.Deregister(this, OnNPCUpdate);
            #endregion
            ((List<RestCommand>) typeof(Rest).GetField("commands", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .GetValue(TShock.RestApi)!)
                .RemoveAll(x => x.Name == "/onlineDuration" || x.Name == "/deathrank");
            #region 指令
            Commands.ChatCommands.RemoveAll(x => x.CommandDelegate == this.Clear || x.CommandDelegate == this.WallQ || x.CommandDelegate == this.RWall || x.CommandDelegate == this.SelfKill || x.CommandDelegate == this.SelfKick || x.CommandDelegate == this.Ghost || x.CommandDelegate == this.JourneyDiff || x.CommandDelegate == this.DeathRank || x.CommandDelegate == this.OnlineRank);
            #endregion
            #region TShcok 钩子
            GetDataHandlers.NewProjectile.UnRegister(this.NewProj);
            GetDataHandlers.ItemDrop.UnRegister(this.OnItemDrop);
            GetDataHandlers.KillMe.UnRegister(this.KillMe);
            GetDataHandlers.PlayerSpawn.UnRegister(this.OnPlayerSpawn);
            GetDataHandlers.PlayerUpdate.UnRegister(this.OnUpdate);
            GeneralHooks.ReloadEvent -= this._reloadHandler;
            #endregion
            CmdHook?.Dispose();
            AccountInfoHook?.Dispose();
            Timer -= this.OnUpdatePlayerOnline;
            On.OTAPI.Hooks.MessageBuffer.InvokeGetData -= this.MessageBuffer_InvokeGetData;

        }
        base.Dispose(disposing);
    }


    //private void NPC_AI1(On.Terraria.NPC.orig_AI orig, NPC self)
    //{
    //    if(Collision.CanHit(self.Center,))
    //}



    private bool MessageBuffer_InvokeGetData(On.OTAPI.Hooks.MessageBuffer.orig_InvokeGetData orig, MessageBuffer instance, ref byte packetId, ref int readOffset, ref int start, ref int length, ref int messageType, int maxPackets)
    {

        if (packetId == (byte) PacketTypes.LoadNetModule)
        {
            using var ms = new MemoryStream(instance.readBuffer);
            ms.Position = readOffset;
            using var reader = new BinaryReader(ms);
            var id = reader.ReadUInt16();
            if (id == Terraria.Net.NetManager.Instance.GetId<Terraria.GameContent.NetModules.NetTextModule>())
            {
                var msg = Terraria.Chat.ChatMessage.Deserialize(reader);
                if (msg.Text.Length > Config.ChatLength)
                {
                    return false;
                }

                if (Regex.IsMatch(msg.Text, @"[\uD800-\uDBFF][\uDC00-\uDFFF]"))
                {
                    return false;
                }
            }
        }
        return orig(instance, ref packetId, ref readOffset, ref start, ref length, ref messageType, maxPackets);
    }


    #region 禁双饰品与肉前第7格饰品位方法
    private void OnUpdate(object? sender, GetDataHandlers.PlayerUpdateEventArgs e)
    {
        if (!Config.KeepArmor || e.Player.HasPermission("servertool.armor.white"))
        {
            return;
        }

        var ArmorGroup = e.Player.TPlayer.armor
            .Take(10)
            .Where(x => x.netID != 0)
            .GroupBy(x => x.netID)
            .Where(x => x.Count() > 1)
            .Select(x => x.First());

        foreach (var keepArmor in ArmorGroup)
        {
            e.Player.SetBuff(156, 180, true);
            TShock.Utils.Broadcast(GetString($"[ServerTools] 玩家 [{e.Player.Name}] 因多饰品被冻结3秒，自动施行清理多饰品装备[i:{keepArmor.netID}]"), Color.DarkRed);
        }
        if (TShock.ServerSideCharacterConfig.Settings.Enabled)
        {
            if (ArmorGroup.Any())
            {
                Utils.ClearItem(ArmorGroup.ToArray(), e.Player);
            }

            if (Config.KeepArmor2 && !Main.hardMode)
            {
                Utils.Clear7Item(e.Player);
            }
        }
    }

    #endregion

    #region NPC保护方法
    private static void OnStrike(NpcStrikeEventArgs args)
    {
        if (!Config.NpcProtect || TShock.Players[args.Player.whoAmI].HasPermission("servertool.npc.strike"))
        {
            return;
        }

        if (Config.NpcProtectList.Contains(args.Npc.netID))
        {
            args.Handled = true;
            TShock.Players[args.Player.whoAmI].SendInfoMessage("[ServerTools] " + args.Npc.FullName + GetString(" 被系统保护"));
        }
    }

    private static void OnNPCUpdate(NpcAiUpdateEventArgs args)
    {
        if (!Config.NpcProtect)
        {
            return;
        }

        if (Config.NpcProtectList.Contains(args.Npc.netID) && args.Npc.active)
        {
            args.Npc.life = args.Npc.lifeMax;
            args.Npc.active = true;
            TSPlayer.All.SendData((PacketTypes) 23, "", args.Npc.whoAmI, 0f, 0f, 0f, 0);
        }
    }
    #endregion

    private static void ViewAccountInfo(CommandArgs args)
    {
        if (args.Parameters.Count < 1)
        {
            args.Player.SendErrorMessage(GetString("语法错误，正确语法: {0}accountinfo <username>."), Commands.Specifier);
            return;
        }

        var username = string.Join(" ", args.Parameters);
        if (!string.IsNullOrWhiteSpace(username))
        {
            var account = TShock.UserAccounts.GetUserAccountByName(username);
            if (account != null)
            {
                var Timezone = TimeZoneInfo.Local.GetUtcOffset(DateTime.Now).Hours.ToString("+#;-#");
                

                if (DateTime.TryParse(account.LastAccessed, out var LastSeen))
                {
                    LastSeen = DateTime.Parse(account.LastAccessed).ToLocalTime();
                    args.Player.SendSuccessMessage(GetString("{0} 最后的登录时间为 {1} {2} UTC{3}."), account.Name, LastSeen.ToShortDateString(),
                        LastSeen.ToShortTimeString(), Timezone);
                }

                if (args.Player.Group.HasPermission(Permissions.advaccountinfo))
                {
                    var KnownIps = JsonConvert.DeserializeObject<List<string>>(account.KnownIps?.ToString() ?? string.Empty);
                    var ip = KnownIps?[^1] ?? "N/A";
                    var Registered = DateTime.Parse(account.Registered).ToLocalTime();
                    args.Player.SendSuccessMessage(GetString("{0} 账户ID为 {1}."), account.Name, account.ID);
                    args.Player.SendSuccessMessage(GetString("{0} 权限组为 {1}."), account.Name, account.Group);
                    args.Player.SendSuccessMessage(GetString("{0} 最后登录使用的IP为 {1}."), account.Name, ip);
                    args.Player.SendSuccessMessage(GetString("{0} 注册时间为 {1} {2} UTC{3}."), account.Name, Registered.ToShortDateString(), Registered.ToShortTimeString(), Timezone);
                }
            }
            else
            {
                args.Player.SendErrorMessage(GetString("用户 {0} 不存在."), username);
            }
        }
        else
        {
            args.Player.SendErrorMessage(GetString("语法错误，正确语法: {0}accountinfo <username>."), Commands.Specifier);
        }
    }

    private void Exit(CommandArgs args)
    {
        args.Player.SendData(PacketTypes.ChestOpen, "", args.Player.Index, -1);
    }

    public static bool CommandHook(TSPlayer ply, string cmd)
    {
        CmdHook.Undo();
        if (ply.GetType() == typeof(TSRestPlayer))
        {
            ply.Account = new()
            {
                Name = ply.Name,
                Group = ply.Group.Name,
                ID = ply.Index
            };
        }
        var status = Commands.HandleCommand(ply, cmd);
        CmdHook.Apply();
        return status;
    }

    private void OnPlayerSpawn(object? sender, GetDataHandlers.SpawnEventArgs e)
    {
        if (e.Player != null)
        {
            Deads.Remove(e.Player);
        }
    }

    private void OnItemDrop(object? sender, GetDataHandlers.ItemDropEventArgs e)
    {
        if (e.Player != null && e.Player.Dead && Config.ClearDrop)
        {
            Main.item[e.ID].active = false;
            e.Handled = true;
            NetMessage.SendData(21, -1, -1, null, e.ID, 0, 0);
        }
    }

    private void OnGreetPlayer(GreetPlayerEventArgs args)
    {
        if (Config.PreventsDeathStateJoin && Main.player[args.Who].dead)
        {
            Task.Run(async () =>
            {
                await Task.Delay(1000);
                TShock.Players[args.Who].SendSuccessMessage(GetString("请在单人模式中结束死亡状态重新进入服务器!"));
                var count = 0;
                while (count < 3)
                {
                    TShock.Players[args.Who].SendSuccessMessage(GetString($"你将在{3 - count}秒后被踢出!"));
                    await Task.Delay(1000);
                    count++;
                }
                TShock.Players[args.Who].Disconnect(GetString("请在单人模式中结束死亡状态重新进入服务器!"));
            });
        }
    }

    private void OnUpdate(EventArgs e)
    {
        TimerCount++;
        if (TimerCount % 60 == 0)
        {
            Timer?.Invoke(e);
            if (Config.DeadTimer)
            {
                foreach (var ply in Deads)
                {
                    if (ply != null && ply.Active && ply.Dead && ply.RespawnTimer > 0)
                    {
                        ply.SendInfoMessage(Config.DeadFormat, ply.RespawnTimer);
                    }
                }
            }

        }
    }

    private void OnLeave(LeaveEventArgs args)
    {
        var ply = TShock.Players[args.Who];
        if (ply == null)
        {
            return;
        }

        Deads.Remove(ply);
        if (ply.Dead)
        {
            this.PlayerDeath[ply.Name] = DateTime.Now.AddSeconds(ply.RespawnTimer);
        }
    }

    private void NewProj(object? sender, GetDataHandlers.NewProjectileEventArgs e)
    {
        if (Main.projectile.Where(x => x != null && x.owner == e.Owner && x.sentry && x.active).Count() > Config.sentryLimit)
        {
            e.Player.Disconnect(GetString($"你因哨兵数量超过{Config.sentryLimit}被踢出"));
        }
        if (e.Player.TPlayer.slotsMinions > Config.summonLimit)
        {
            e.Player.Disconnect(GetString($"你因召唤物数量超过{Config.summonLimit}被踢出"));
        }
        if (Main.projectile[e.Index].bobber && Config.MultipleFishingRodsAreProhibited && Config.ForbiddenBuoys.FindAll(f => f == e.Type).Count > 0)
        {
            var bobber = Main.projectile.Where(f => f != null && f.owner == e.Owner && f.active && f.type == e.Type);
            if (bobber.Count() > 2)
            {
                e.Player.SendErrorMessage(GetString("你因多鱼线被石化3秒钟!"));
                e.Player.SetBuff(156, 180, true);
            }
        }
    }

    private void HandleCommandLine(string[] param)
    {
        var args = Terraria.Utils.ParseArguements(param);
        foreach (var key in args)
        {
            switch (key.Key.ToLower())
            {
                case "-seed":
                    Main.AutogenSeedName = key.Value;
                    break;
            }
        }
    }

    public void LoadConfig()
    {
        if (File.Exists(this.PATH))
        {
            try
            {
                Config = Config.Read(this.PATH);
            }
            catch (Exception e)
            {

                TShock.Log.ConsoleError(GetString("ServerTools.json配置读取错误:{0}"), e.ToString());
            }
        }
        else
        {
            Config.ForbiddenBuoys = new List<short>() { 360, 361, 362, 363, 364, 365, 366, 381, 382, 760, 775, 986, 987, 988, 989, 990, 991, 992, 993 };
            Config.NpcProtectList = new List<int>() { 17, 18, 19, 20, 38, 105, 106, 107, 108, 160, 123, 124, 142, 207, 208, 227, 228, 229, 353, 354, 376, 441, 453, 550, 579, 588, 589, 633, 663, 678, 679, 680, 681, 682, 683, 684, 685, 686, 687, 375, 442, 443, 539, 444, 445, 446, 447, 448, 605, 627, 601, 613 };
        }
        Config.Write(this.PATH);
    }

    public bool SetJourneyDiff(string diffName)
    {
        float diff;
        switch (diffName.ToLower())
        {
            case "master":
                diff = 1f;
                break;
            case "journey":
                diff = 0f;
                break;
            case "normal":
                diff = 0.33f;
                break;
            case "expert":
                diff = 0.66f;
                break;
            default:
                return false;
        }
        var power = CreativePowerManager.Instance.GetPower<CreativePowers.DifficultySliderPower>();
        power._sliderCurrentValueCache = diff;
        power.UpdateInfoFromSliderValueCache();
        power.OnPlayerJoining(0);
        return true;
    }

    private void PostInitialize(EventArgs args)
    {
        //设置世界模式
        if (Config.SetWorldMode)
        {
            if (Config.WorldMode > 1 && Config.WorldMode < 4)
            {
                Main.GameMode = Config.WorldMode;
                TSPlayer.All.SendData(PacketTypes.WorldInfo);
            }
        }
        //旅途难度
        if (Main._currentGameModeInfo.IsJourneyMode && Config.SetJourneyDifficult)
        {
            this.SetJourneyDiff(Config.JourneyMode);
        }
    }

    private void GetData(GetDataEventArgs args)
    {
        var ply = TShock.Players[args.Msg.whoAmI];
        if (args.Handled || ply == null)
        {
            return;
        }

        if (Config.PickUpMoney && args.MsgID == PacketTypes.SyncExtraValue)
        {
            using BinaryReader reader = new(new MemoryStream(args.Msg.readBuffer, args.Index, args.Length));
            var npcid = reader.ReadInt16();
            var money = reader.ReadInt32();
            var x = reader.ReadSingle();
            var y = reader.ReadSingle();
            ply.SendData(PacketTypes.SyncExtraValue, "", npcid, 0, x, y);
            args.Handled = true;
            return;
        }


        if (Config.KeepOpenChest && args.MsgID == PacketTypes.ChestOpen)
        {

            using BinaryReader binaryReader5 = new(new MemoryStream(args.Msg.readBuffer, args.Index, args.Length));
            var ChestId = binaryReader5.ReadInt16();
            if (ChestId != -1 && ply.ActiveChest != -1)
            {
                ply.ActiveChest = -1;
                ply.SendData(PacketTypes.ChestOpen, "", -1);
                ply.SendErrorMessage(GetString("禁止双箱!"));
                args.Handled = true;
            }
        }

        if (args.MsgID == PacketTypes.ChestGetContents && Config.KeepOpenChest)
        {
            if (ply.ActiveChest != -1)
            {
                ply.ActiveChest = -1;
                ply.SendData(PacketTypes.ChestOpen, "", -1);
                ply.SendErrorMessage(GetString("禁止双箱!"));
                args.Handled = true;
            }
        }
    }

    private void OnInitialize(EventArgs args)
    {
        //执行命令
        if (TShock.UserAccounts.GetUserAccounts().Count == 0 && Config.ResetExecCommands.Length > 0)
        {
            for (var i = 0; i < Config.ResetExecCommands.Length; i++)
            {
                Commands.HandleCommand(TSPlayer.Server, Config.ResetExecCommands[i]);
            }
        }
    }

    private void OnJoin(JoinEventArgs args)
    {
        if (args.Handled)
        {
            return;
        }

        if (Config.OnlySoftCoresAreAllowed)
        {
            if (TShock.Players[args.Who].Difficulty != 0)
            {
                TShock.Players[args.Who].Disconnect(GetString("仅允许软核进入!"));
            }
        }
        if (Config.BlockUnregisteredEntry)
        {
            //阻止未注册进入
            if (args.Who == -1 || TShock.Players[args.Who] == null || TShock.UserAccounts.GetUserAccountsByName(TShock.Players[args.Who].Name).Count == 0)
            {
                TShock.Players[args.Who].Disconnect(Config.BlockEntryStatement);
            }
        }
        if (Config.DeathLast && TShock.Players[args.Who] != null)
        {
            if (this.PlayerDeath.TryGetValue(TShock.Players[args.Who].Name, out var time))
            {
                var respawn = time - DateTime.Now;
                if (respawn.TotalSeconds > 0)
                {
                    TShock.Players[args.Who].Disconnect(GetString($"退出服务器时处于死亡状态！\n请等待死亡结束，还有{respawn.TotalSeconds}秒结束！"));
                }
            }
        }
    }
}