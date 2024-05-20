﻿using Economics.Skill.Enumerates;
using Economics.Skill.Model;
using Economics.Skill.Model.Options;
using EconomicsAPI.Extensions;
using Microsoft.Xna.Framework;
using Terraria;
using TShockAPI;

namespace Economics.Skill;

public class Utils
{
    public static SkillContext VerifyBindSkill(TSPlayer Player, int index)
    {
        var context = Skill.Config.GetSkill(index) ?? throw new NullReferenceException($"技能序号{index} 不存在！");
        if (context.SkillSpark.SparkMethod.Contains(SkillSparkType.Take) && Player.SelectedItem.netID == 0 || Player.SelectedItem.stack == 0)
            throw new Exception("这是一个主动技能，请手持一个有效武器!");
        if (RPG.RPG.InLevel(Player.Name, context.LimitLevel))
            throw new Exception($"你当前等级无法购买此技能，限制等级:{string.Join(", ", context.LimitLevel)}");
        if (Player.InProgress(context.LimitProgress))
            throw new Exception($"当前进度无法购买此技能，限制进度:{string.Join(", ", context.LimitProgress)}");
        var bind = Skill.PlayerSKillManager.QuerySkillByItem(Player.Name, Player.SelectedItem.netID);
        if (context.SkillUnique && Skill.PlayerSKillManager.HasSkill(Player.Name, index))
            throw new Exception("此技能是唯一的不能重复绑定!");
        if (context.SkillUniqueAll && Skill.PlayerSKillManager.HasSkill(index))
            throw new Exception("此技能全服唯一已经有其他人绑定了此技能!");
        if (bind.Count >= Skill.Config.SkillMaxCount)
            throw new Exception("技能已超过规定的最大绑定数量!");
        if (bind.Count >= Skill.Config.WeapoeBindMaxCount)
            throw new Exception("此武器已超过规定的最大绑定数量!");
        return context;
    }

    /// <summary>
    /// 通用技能触发器
    /// </summary>
    /// <param name="Player"></param>
    /// <param name="skill"></param>
    public static void EmitGeneralSkill(TSPlayer Player, SkillContext skill)
    {
        if (!string.IsNullOrEmpty(skill.Broadcast))
            TShock.Utils.Broadcast(skill.Broadcast, Color.Wheat);
        Player.StrikeNpc(skill.StrikeNpc.Damage, skill.StrikeNpc.Range);
        Player.ExecRangeCommands(skill.ExecCommand.Range, skill.ExecCommand.Commands);
        Player.HealAllLife(skill.HealPlayerHPOption.Range, skill.HealPlayerHPOption.HP);
        Player.ClearProj(skill.ClearProjectile.Range);
        Player.CollectNPC(skill.PullNpc.Range, Skill.Config.BanPullNpcs);
    }

    /// <summary>
    /// 圆弧技能触发器
    /// </summary>
    /// <param name="Player"></param>
    /// <param name="circles"></param>
    /// <param name="pos"></param>
    public static void SpawnPointsOnArcProj(TSPlayer Player, List<CircleProjectile> circles, Vector2 pos)
    {
        Task.Run(() =>
        {
            foreach (var circle in circles)
            {
                if (circle.Enable)
                {
                    var posed = pos.GetArcPoints(circle.StartAngle, circle.EndAngle, circle.Radius, circle.Interval);
                    var reverse = circle.Reverse ? -1 : 1;
                    foreach (var vec in posed)
                    {
                        var angle = vec.AngleFrom(pos);
                        Vector2 vel;
                        if (!circle.FollowPlayer)
                            vel = vec + new Vector2(circle.X, circle.Y);
                        else
                            vel = Player.TPlayer.Center + Player.TPlayer.ItemOffSet() + new Vector2(circle.X, circle.Y);
                        var radiusvel = vec.RotatedBy(angle).ToLenOf(circle.Speed) * reverse;
                        int index = EconomicsAPI.Utils.Projectile.NewProjectile(
                            //发射原无期
                            Player.TPlayer.GetProjectileSource_Item(Player.TPlayer.HeldItem),
                            //发射位置
                            vel,
                            radiusvel,
                            circle.ID,
                            circle.Damage,
                            circle.Knockback,
                            Player.Index);
                        TSPlayer.All.SendData(PacketTypes.ProjectileNew, "", index);
                    }
                    Task.Delay(circle.Dealy).Wait();
                }
            }
        });
    }

    /// <summary>
    /// 技能触发器
    /// </summary>
    /// <param name="Player"></param>
    /// <param name="skill"></param>
    /// <param name="vel"></param>
    /// <param name="pos"></param>
    public static void SpawnSkillProjectile(TSPlayer Player, SkillContext skill, Vector2 vel, Vector2 pos)
    {
        EmitGeneralSkill(Player, skill);
        foreach (var proj in skill.Projectiles)
        {
            SpawnPointsOnArcProj(Player, proj.CircleProjectiles, pos);
            Task.Run(() =>
            {
                foreach (var opt in proj.ProjectileCycle.ProjectileCycles)
                {
                    var _vel = vel.RotationAngle(proj.Angle).ToLenOf(proj.Speed);
                    var _pos = pos + new Vector2(proj.X, proj.Y);
                    for (int i = 0; i < opt.Count; i++)
                    {
                        #region 生成弹幕
                        int index = EconomicsAPI.Utils.Projectile.NewProjectile(
                            //发射原无期
                            Player.TPlayer.GetProjectileSource_Item(Player.TPlayer.HeldItem),
                            //发射位置
                            _pos,
                            _vel,
                            proj.ID,
                            proj.Damage,
                            proj.Knockback,
                            Player.Index);
                        TSPlayer.All.SendData(PacketTypes.ProjectileNew, "", index);
                        #endregion

                        #region 数值重置
                        _vel = _vel.RotationAngle(opt.GrowAngle).ToLenOf(proj.Speed);
                        if (opt.FollowPlayer)
                            _pos = Player.TPlayer.Center + Player.TPlayer.ItemOffSet() + new Vector2(opt.GrowX, opt.GrowY);
                        else
                            _pos += new Vector2(opt.GrowX, opt.GrowY);
                        #endregion
                        Task.Delay(opt.Dealy).Wait();
                    }
                }
            });
        }
    }
    /// <summary>
    /// 释放技能
    /// </summary>
    /// <param name="Player"></param>
    /// <param name="skill"></param>
    public static void EmitSkill(TSPlayer Player, SkillContext skill)
    {
        //原始发射位置
        var pos = Player.TPlayer.Center + Player.TPlayer.ItemOffSet();
        //原始角度速度参数
        var vel = Player.TPlayer.ItemOffSet();
        SpawnSkillProjectile(Player, skill, vel, pos);
    }

    public static void EmitSkill(GetDataHandlers.NewProjectileEventArgs e, SkillContext skill)
    {
        //原始发射位置
        var pos = e.Position;
        //原始角度速度参数
        var vel = e.Velocity;
        SpawnSkillProjectile(e.Player, skill, vel, pos);
    }
}
