﻿using Terraria;
using Terraria.ID;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;
using static TShockAPI.GetDataHandlers;

namespace LifemaxExtra;

[ApiVersion(2, 1)]
public class LifemaxExtra : TerrariaPlugin
{
    public override string Author => "佚名 & 肝帝熙恩 & 少司命";
    public override string Description => "提升生命值上限";
    public override string Name => "LifemaxExtra";
    public override Version Version => new Version(1, 0, 0, 7);
    public static Configuration Config = null!;

    public LifemaxExtra(Main game) : base(game)
    {
    }

    private static void LoadConfig()
    {
        Config = Configuration.Read(Configuration.FilePath);
        Config.Write(Configuration.FilePath);
    }

    private static void ReloadConfig(ReloadEventArgs args)
    {
        LoadConfig();
        args.Player.SendSuccessMessage("[{0}] 重新加载配置完毕。", typeof(LifemaxExtra).Name);
    }

    public override void Initialize()
    {
        Commands.ChatCommands.Add(new Command("lifemaxextra.use", this.HP, "hp"));
        Commands.ChatCommands.Add(new Command("lifemaxextra.use", this.Mana, "mp"));
        GeneralHooks.ReloadEvent += ReloadConfig;
        GetDataHandlers.PlayerUpdate += this.OnPlayerUpdate;
        GetDataHandlers.PlayerHP += this.OnHP;
        GetDataHandlers.PlayerMana += this.OnMana;
        LoadConfig();
    }


    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Commands.ChatCommands.RemoveAll(x => x.CommandDelegate == this.HP || x.CommandDelegate == this.Mana);
            GeneralHooks.ReloadEvent -= ReloadConfig;
            GetDataHandlers.PlayerUpdate -= this.OnPlayerUpdate;
            GetDataHandlers.PlayerHP -= this.OnHP;
            GetDataHandlers.PlayerMana -= this.OnMana;
        }
        base.Dispose(disposing);
    }

    private void Mana(CommandArgs args)
    {
        if (args.Parameters.Count == 3 && args.Parameters[0].ToLower() == "enh")
        {
            var plys = TSPlayer.FindByNameOrID(args.Parameters[1]);
            if (plys.Count > 0)
            {
                if (int.TryParse(args.Parameters[2], out var hp) && hp >= 0)
                {
                    this.SetPlayerMana(plys[0], hp, true);
                    args.Player.SendSuccessMessage($"成功为玩家`{plys[0].Name}`提高{hp}魔法上限!");
                }
                else
                {
                    args.Player.SendErrorMessage("输入的数值有误!");
                }
            }
            else
            {
                args.Player.SendErrorMessage("目标玩家不在线!");
            }
        }
        else if (args.Parameters.Count == 3 && args.Parameters[0].ToLower() == "set")
        {
            var plys = TSPlayer.FindByNameOrID(args.Parameters[1]);
            if (plys.Count > 0)
            {
                if (int.TryParse(args.Parameters[2], out var hp) && hp >= 0)
                {
                    this.SetPlayerMana(plys[0], hp);
                    args.Player.SendSuccessMessage($"成功设置玩家`{plys[0].Name}`魔法上限!");
                }
                else
                {
                    args.Player.SendErrorMessage("输入的数值有误!");
                }
            }
            else
            {
                args.Player.SendErrorMessage("目标玩家不在线!");
            }
        }
        else if (args.Parameters.Count == 2 && args.Parameters[0].ToLower() == "enh")
        {
            if (int.TryParse(args.Parameters[1], out var hp) && hp >= 0)
            {
                this.SetPlayerMana(args.Player, hp, true);
                args.Player.SendSuccessMessage($"成功为玩家`{args.Player.Name}`提高{hp}魔法上限!");
            }
            else
            {
                args.Player.SendErrorMessage("输入的数值有误!");
            }
        }
        else if (args.Parameters.Count == 2 && args.Parameters[0].ToLower() == "set")
        {
            if (int.TryParse(args.Parameters[1], out var hp) && hp >= 0)
            {
                this.SetPlayerMana(args.Player, hp);
                args.Player.SendSuccessMessage($"成功设置玩家`{args.Player.Name}`魔法上限{hp}!");
            }
            else
            {
                args.Player.SendErrorMessage("输入的数值有误!");
            }
        }
        else
        {
            args.Player.SendErrorMessage("语法错误");
            args.Player.SendInfoMessage("/mp enh <玩家> <提升数值>");
            args.Player.SendInfoMessage("/mp set <玩家> <数值>");
            args.Player.SendInfoMessage("/mp enh <提升数值>");
            args.Player.SendInfoMessage("/mp set <数值>");
        }

    }

    private void OnMana(object? sender, PlayerManaEventArgs e)
    {
        if (e.Player.IsLoggedIn && e.Max > Config.MaxMP)
        {
            e.Player.TPlayer.statManaMax = Config.MaxMP;
            e.Player.SendData(PacketTypes.PlayerMana, "", e.Player.Index);
            e.Player.SendInfoMessage("最大蓝量不得超过{0}!", Config.MaxMP);
        }
    }



    private void OnHP(object? sender, PlayerHPEventArgs e)
    {
        if (e.Player.IsLoggedIn && e.Max > Config.MaxHP)
        {
            e.Player.TPlayer.statLifeMax = Config.MaxHP;
            e.Player.SendData(PacketTypes.PlayerHp, "", e.Player.Index);
            e.Player.SendInfoMessage("最大血量不得超过{0}!", Config.MaxHP);
        }
    }

    private void SetPlayerMana(TSPlayer ply, int Mana, bool enh = false)
    {
        var MaxMP = ply.TPlayer.statManaMax;
        int raise;
        int currency;
        if (Mana > short.MaxValue || MaxMP + Mana > short.MaxValue)
        {
            raise = short.MaxValue - MaxMP;
            currency = short.MaxValue;
            ply.SendErrorMessage("生命值无法大于{0}!", short.MaxValue);
        }
        else
        {
            if (enh)
            {
                raise = Mana;
                currency = MaxMP + Mana;
            }
            else
            {
                raise = Mana - MaxMP;
                currency = Mana;
            }
        }
        ply.TPlayer.statManaMax = currency;
        ply.TPlayer.statManaMax2 = currency;
        ply.TPlayer.statMana = currency;
        ply.SendData(PacketTypes.PlayerMana, null, ply.Index);
        ply.SendData(PacketTypes.EffectMana, null, ply.Index, raise);
    }

    private void SetPlayerHP(TSPlayer ply, int HP, bool enh = false)
    {
        var MaxHP = ply.TPlayer.statLifeMax;
        int raise;
        int currency;
        if (HP > short.MaxValue || MaxHP + HP > short.MaxValue)
        {
            raise = short.MaxValue - MaxHP;
            currency = short.MaxValue;
            ply.SendErrorMessage("生命值无法大于{0}!", short.MaxValue);
        }
        else
        {
            if (enh)
            {
                raise = HP;
                currency = MaxHP + HP;
            }
            else
            {
                raise = HP - MaxHP;
                currency = HP;
            }
        }
        ply.TPlayer.statLifeMax = currency;
        ply.TPlayer.statLifeMax2 = currency;
        ply.TPlayer.statLife = currency;
        ply.SendData(PacketTypes.PlayerHp, null, ply.Index);
        ply.SendData(PacketTypes.EffectHeal, null, ply.Index, raise);
    }


    private void HP(CommandArgs args)
    {
        if (args.Parameters.Count == 3 && args.Parameters[0].ToLower() == "enh")
        {
            var plys = TSPlayer.FindByNameOrID(args.Parameters[1]);
            if (plys.Count > 0)
            {
                if (int.TryParse(args.Parameters[2], out var hp) && hp >= 0)
                {
                    this.SetPlayerHP(plys[0], hp, true);
                    args.Player.SendSuccessMessage($"成功为玩家`{plys[0].Name}`提高{hp}血量上限!");
                }
                else
                {
                    args.Player.SendErrorMessage("输入的数值有误!");
                }
            }
            else
            {
                args.Player.SendErrorMessage("目标玩家不在线!");
            }
        }
        else if (args.Parameters.Count == 3 && args.Parameters[0].ToLower() == "set")
        {
            var plys = TSPlayer.FindByNameOrID(args.Parameters[1]);
            if (plys.Count > 0)
            {
                if (int.TryParse(args.Parameters[2], out var hp) && hp >= 0)
                {
                    this.SetPlayerHP(plys[0], hp);
                    args.Player.SendSuccessMessage($"成功设置玩家`{plys[0].Name}`血量上限!");
                }
                else
                {
                    args.Player.SendErrorMessage("输入的数值有误!");
                }
            }
            else
            {
                args.Player.SendErrorMessage("目标玩家不在线!");
            }
        }
        else if (args.Parameters.Count == 2 && args.Parameters[0].ToLower() == "enh")
        {
            if (int.TryParse(args.Parameters[1], out var hp) && hp >= 0)
            {
                this.SetPlayerHP(args.Player, hp, true);
                args.Player.SendSuccessMessage($"成功为玩家`{args.Player.Name}`提高{hp}血量上限!");
            }
            else
            {
                args.Player.SendErrorMessage("输入的数值有误!");
            }
        }
        else if (args.Parameters.Count == 2 && args.Parameters[0].ToLower() == "set")
        {
            if (int.TryParse(args.Parameters[1], out var hp) && hp >= 0)
            {
                this.SetPlayerHP(args.Player, hp);
                args.Player.SendSuccessMessage($"成功设置玩家`{args.Player.Name}`血量上限{hp}!");
            }
            else
            {
                args.Player.SendErrorMessage("输入的数值有误!");
            }
        }
        else
        {
            args.Player.SendErrorMessage("语法错误");
            args.Player.SendInfoMessage("/hp enh <玩家> <提升数值>");
            args.Player.SendInfoMessage("/hp set <玩家> <数值>");
            args.Player.SendInfoMessage("/hp enh <提升数值>");
            args.Player.SendInfoMessage("/hp set <数值>");
        }
    }


    public void UseItemRaiseHP(TSPlayer Player, int hp)
    {
        Player.SelectedItem.stack--; // 减少玩家背包中选定物品的堆叠数量
        Player.SendData(PacketTypes.PlayerSlot, "", Player.Index, Player.TPlayer.selectedItem);
        this.SetPlayerHP(Player, hp, true);
        Player.TPlayer.ApplyItemTime(Player.SelectedItem);
    }

    public void UseItemRaiseMP(TSPlayer Player, int mp)
    {
        Player.SelectedItem.stack--; // 减少玩家背包中选定物品的堆叠数量
        Player.SendData(PacketTypes.PlayerSlot, "", Player.Index, Player.TPlayer.selectedItem);
        this.SetPlayerMana(Player, mp, true);
        Player.TPlayer.ApplyItemTime(Player.SelectedItem);
    }

    private void OnPlayerUpdate(object? sender, PlayerUpdateEventArgs args)
    {
        if (args.Player.TPlayer.ItemTimeIsZero && args.Player.TPlayer.controlUseItem)
        {
            if (Config.ItemRaiseHP.TryGetValue(args.Player.SelectedItem.type, out var raiseHp))
            {
                if (args.Player.TPlayer.statLifeMax < raiseHp.Max)
                {
                    switch (args.Player.SelectedItem.type)
                    {
                        case ItemID.LifeCrystal:
                            if (args.Player.TPlayer.statLifeMax >= 400)
                            {
                                this.UseItemRaiseHP(args.Player, raiseHp.Raise);
                            }
                            break;
                        case ItemID.LifeFruit:
                            if (args.Player.TPlayer.statLifeMax >= 500)
                            {
                                this.UseItemRaiseHP(args.Player, raiseHp.Raise);
                            }
                            break;
                        default:
                            this.UseItemRaiseHP(args.Player, raiseHp.Raise);
                            break;
                    }
                }
            }

            if (Config.ItemRaiseMP.TryGetValue(args.Player.SelectedItem.type, out var raiseMp))
            {
                if (args.Player.TPlayer.statManaMax < raiseMp.Max)
                {
                    switch (args.Player.SelectedItem.type)
                    {
                        case ItemID.ManaCrystal:
                            if (args.Player.TPlayer.statManaMax >= 200)
                            {
                                this.UseItemRaiseMP(args.Player, raiseMp.Raise);
                            }
                            break;
                        default:
                            this.UseItemRaiseMP(args.Player, raiseMp.Raise);
                            break;
                    }
                }
            }
        }
    }

}