using Xunit;

namespace Hybrasyl.Tests;

[Collection("Hybrasyl")]
public class Stats
{
    private static HybrasylFixture Fixture;

    public Stats(HybrasylFixture fixture)
    {
        Fixture = fixture;
    }

    [Fact]
    public void CreateAndSerialize()
    {
        Fixture.ResetUserStats();
        Fixture.TestUser.Save();
        Assert.True(Game.World.WorldState.TryGetUser(Fixture.TestUser.Name, out var deserializedUser));
        Assert.True(Fixture.TestUser.Stats.BonusHp == deserializedUser.Stats.BonusHp,
            $"BonusHp should be {Fixture.TestUser.Stats.BonusHp}, is {deserializedUser.Stats.BonusHp}");
        Assert.True(Fixture.TestUser.Stats.BaseHp == deserializedUser.Stats.BaseHp,
            $"BaseHp should be {Fixture.TestUser.Stats.BaseHp}, is {deserializedUser.Stats.BaseHp}");
        Assert.True(Fixture.TestUser.Stats.BonusMp == deserializedUser.Stats.BonusMp,
            $"BonusMp should be {Fixture.TestUser.Stats.BonusMp}, is {deserializedUser.Stats.BonusMp}");
        Assert.True(Fixture.TestUser.Stats.BaseMp == deserializedUser.Stats.BaseMp,
            $"BaseMp should be {Fixture.TestUser.Stats.BaseMp}, is {deserializedUser.Stats.BaseMp}");
        Assert.True(Fixture.TestUser.Stats.BonusStr == deserializedUser.Stats.BonusStr,
            $"BonusStr should be {Fixture.TestUser.Stats.BonusStr}, is {deserializedUser.Stats.BonusStr}");
        Assert.True(Fixture.TestUser.Stats.BaseStr == deserializedUser.Stats.BaseStr,
            $"BaseStr should be {Fixture.TestUser.Stats.BaseStr}, is {deserializedUser.Stats.BaseStr}");
        Assert.True(Fixture.TestUser.Stats.BonusCon == deserializedUser.Stats.BonusCon,
            $"BonusCon should be {Fixture.TestUser.Stats.BonusCon}, is {deserializedUser.Stats.BonusCon}");
        Assert.True(Fixture.TestUser.Stats.BaseCon == deserializedUser.Stats.BaseCon,
            $"BaseCon should be {Fixture.TestUser.Stats.BaseCon}, is {deserializedUser.Stats.BaseCon}");
        Assert.True(Fixture.TestUser.Stats.BonusDex == deserializedUser.Stats.BonusDex,
            $"BonusDex should be {Fixture.TestUser.Stats.BonusDex}, is {deserializedUser.Stats.BonusDex}");
        Assert.True(Fixture.TestUser.Stats.BaseDex == deserializedUser.Stats.BaseDex,
            $"BaseDex should be {Fixture.TestUser.Stats.BaseDex}, is {deserializedUser.Stats.BaseDex}");
        Assert.True(Fixture.TestUser.Stats.BonusInt == deserializedUser.Stats.BonusInt,
            $"BonusInt should be {Fixture.TestUser.Stats.BonusInt}, is {deserializedUser.Stats.BonusInt}");
        Assert.True(Fixture.TestUser.Stats.BaseInt == deserializedUser.Stats.BaseInt,
            $"BaseInt should be {Fixture.TestUser.Stats.BaseInt}, is {deserializedUser.Stats.BaseInt}");
        Assert.True(Fixture.TestUser.Stats.BonusWis == deserializedUser.Stats.BonusWis,
            $"BonusWis should be {Fixture.TestUser.Stats.BonusWis}, is {deserializedUser.Stats.BonusWis}");
        Assert.True(Fixture.TestUser.Stats.BaseWis == deserializedUser.Stats.BaseWis,
            $"BaseWis should be {Fixture.TestUser.Stats.BaseWis}, is {deserializedUser.Stats.BaseWis}");
        Assert.True(Fixture.TestUser.Stats.BonusCrit == deserializedUser.Stats.BonusCrit,
            $"BonusCrit should be {Fixture.TestUser.Stats.BonusCrit}, is {deserializedUser.Stats.BonusCrit}");
        Assert.True(Fixture.TestUser.Stats.BaseCrit == deserializedUser.Stats.BaseCrit,
            $"BaseCrit should be {Fixture.TestUser.Stats.BaseCrit}, is {deserializedUser.Stats.BaseCrit}");
        Assert.True(Fixture.TestUser.Stats.BonusMagicCrit == deserializedUser.Stats.BonusMagicCrit,
            $"BonusMagicCrit should be {Fixture.TestUser.Stats.BonusMagicCrit}, is {deserializedUser.Stats.BonusMagicCrit}");
        Assert.True(Fixture.TestUser.Stats.BaseMagicCrit == deserializedUser.Stats.BaseMagicCrit,
            $"BaseMagicCrit should be {Fixture.TestUser.Stats.BaseMagicCrit}, is {deserializedUser.Stats.BaseMagicCrit}");
        Assert.True(Fixture.TestUser.Stats.BonusDmg == deserializedUser.Stats.BonusDmg,
            $"BonusDmg should be {Fixture.TestUser.Stats.BonusDmg}, is {deserializedUser.Stats.BonusDmg}");
        Assert.True(Fixture.TestUser.Stats.BaseDmg == deserializedUser.Stats.BaseDmg,
            $"BaseDmg should be {Fixture.TestUser.Stats.BaseDmg}, is {deserializedUser.Stats.BaseDmg}");
        Assert.True(Fixture.TestUser.Stats.BonusHit == deserializedUser.Stats.BonusHit,
            $"BonusHit should be {Fixture.TestUser.Stats.BonusHit}, is {deserializedUser.Stats.BonusHit}");
        Assert.True(Fixture.TestUser.Stats.BaseHit == deserializedUser.Stats.BaseHit,
            $"BaseHit should be {Fixture.TestUser.Stats.BaseHit}, is {deserializedUser.Stats.BaseHit}");
        Assert.True(Fixture.TestUser.Stats.BonusAc == deserializedUser.Stats.BonusAc,
            $"BonusAc should be {Fixture.TestUser.Stats.BonusAc}, is {deserializedUser.Stats.BonusAc}");
        Assert.True(Fixture.TestUser.Stats.BaseAc == deserializedUser.Stats.BaseAc,
            $"BaseAc should be {Fixture.TestUser.Stats.BaseAc}, is {deserializedUser.Stats.BaseAc}");
        Assert.True(Fixture.TestUser.Stats.BonusMr == deserializedUser.Stats.BonusMr,
            $"BonusMr should be {Fixture.TestUser.Stats.BonusMr}, is {deserializedUser.Stats.BonusMr}");
        Assert.True(Fixture.TestUser.Stats.BaseMr == deserializedUser.Stats.BaseMr,
            $"BaseMr should be {Fixture.TestUser.Stats.BaseMr}, is {deserializedUser.Stats.BaseMr}");
        Assert.True(Fixture.TestUser.Stats.BonusRegen == deserializedUser.Stats.BonusRegen,
            $"BonusRegen should be {Fixture.TestUser.Stats.BonusRegen}, is {deserializedUser.Stats.BonusRegen}");
        Assert.True(Fixture.TestUser.Stats.BaseRegen == deserializedUser.Stats.BaseRegen,
            $"BaseRegen should be {Fixture.TestUser.Stats.BaseRegen}, is {deserializedUser.Stats.BaseRegen}");
        Assert.True(
            Fixture.TestUser.Stats.BonusInboundDamageModifier == deserializedUser.Stats.BonusInboundDamageModifier,
            $"BonusInboundDamageModifier should be {Fixture.TestUser.Stats.BonusInboundDamageModifier}, is {deserializedUser.Stats.BonusInboundDamageModifier}");
        Assert.True(
            Fixture.TestUser.Stats.BaseInboundDamageModifier == deserializedUser.Stats.BaseInboundDamageModifier,
            $"BaseInboundDamageModifier should be {Fixture.TestUser.Stats.BaseInboundDamageModifier}, is {deserializedUser.Stats.BaseInboundDamageModifier}");
        Assert.True(Fixture.TestUser.Stats.BonusInboundHealModifier == deserializedUser.Stats.BonusInboundHealModifier,
            $"BonusInboundHealModifier should be {Fixture.TestUser.Stats.BonusInboundHealModifier}, is {deserializedUser.Stats.BonusInboundHealModifier}");
        Assert.True(Fixture.TestUser.Stats.BaseInboundHealModifier == deserializedUser.Stats.BaseInboundHealModifier,
            $"BaseInboundHealModifier should be {Fixture.TestUser.Stats.BaseInboundHealModifier}, is {deserializedUser.Stats.BaseInboundHealModifier}");
        Assert.True(
            Fixture.TestUser.Stats.BonusOutboundDamageModifier == deserializedUser.Stats.BonusOutboundDamageModifier,
            $"BonusOutboundDamageModifier should be {Fixture.TestUser.Stats.BonusOutboundDamageModifier}, is {deserializedUser.Stats.BonusOutboundDamageModifier}");
        Assert.True(
            Fixture.TestUser.Stats.BaseOutboundDamageModifier == deserializedUser.Stats.BaseOutboundDamageModifier,
            $"BaseOutboundDamageModifier should be {Fixture.TestUser.Stats.BaseOutboundDamageModifier}, is {deserializedUser.Stats.BaseOutboundDamageModifier}");
        Assert.True(
            Fixture.TestUser.Stats.BonusOutboundHealModifier == deserializedUser.Stats.BonusOutboundHealModifier,
            $"BonusOutboundHealModifier should be {Fixture.TestUser.Stats.BonusOutboundHealModifier}, is {deserializedUser.Stats.BonusOutboundHealModifier}");
        Assert.True(Fixture.TestUser.Stats.BaseOutboundHealModifier == deserializedUser.Stats.BaseOutboundHealModifier,
            $"BaseOutboundHealModifier should be {Fixture.TestUser.Stats.BaseOutboundHealModifier}, is {deserializedUser.Stats.BaseOutboundHealModifier}");
        Assert.True(Fixture.TestUser.Stats.BonusReflectMagical == deserializedUser.Stats.BonusReflectMagical,
            $"BonusReflectMagical should be {Fixture.TestUser.Stats.BonusReflectMagical}, is {deserializedUser.Stats.BonusReflectMagical}");
        Assert.True(Fixture.TestUser.Stats.BaseReflectMagical == deserializedUser.Stats.BaseReflectMagical,
            $"BaseReflectMagical should be {Fixture.TestUser.Stats.BaseReflectMagical}, is {deserializedUser.Stats.BaseReflectMagical}");
        Assert.True(Fixture.TestUser.Stats.BonusReflectPhysical == deserializedUser.Stats.BonusReflectPhysical,
            $"BonusReflectPhysical should be {Fixture.TestUser.Stats.BonusReflectPhysical}, is {deserializedUser.Stats.BonusReflectPhysical}");
        Assert.True(Fixture.TestUser.Stats.BaseReflectPhysical == deserializedUser.Stats.BaseReflectPhysical,
            $"BaseReflectPhysical should be {Fixture.TestUser.Stats.BaseReflectPhysical}, is {deserializedUser.Stats.BaseReflectPhysical}");
        Assert.True(Fixture.TestUser.Stats.BonusExtraGold == deserializedUser.Stats.BonusExtraGold,
            $"BonusExtraGold should be {Fixture.TestUser.Stats.BonusExtraGold}, is {deserializedUser.Stats.BonusExtraGold}");
        Assert.True(Fixture.TestUser.Stats.BaseExtraGold == deserializedUser.Stats.BaseExtraGold,
            $"BaseExtraGold should be {Fixture.TestUser.Stats.BaseExtraGold}, is {deserializedUser.Stats.BaseExtraGold}");
        Assert.True(Fixture.TestUser.Stats.BonusDodge == deserializedUser.Stats.BonusDodge,
            $"BonusDodge should be {Fixture.TestUser.Stats.BonusDodge}, is {deserializedUser.Stats.BonusDodge}");
        Assert.True(Fixture.TestUser.Stats.BaseDodge == deserializedUser.Stats.BaseDodge,
            $"BaseDodge should be {Fixture.TestUser.Stats.BaseDodge}, is {deserializedUser.Stats.BaseDodge}");
        Assert.True(Fixture.TestUser.Stats.BonusMagicDodge == deserializedUser.Stats.BonusMagicDodge,
            $"BonusMagicDodge should be {Fixture.TestUser.Stats.BonusMagicDodge}, is {deserializedUser.Stats.BonusMagicDodge}");
        Assert.True(Fixture.TestUser.Stats.BaseMagicDodge == deserializedUser.Stats.BaseMagicDodge,
            $"BaseMagicDodge should be {Fixture.TestUser.Stats.BaseMagicDodge}, is {deserializedUser.Stats.BaseMagicDodge}");
        Assert.True(Fixture.TestUser.Stats.BonusExtraXp == deserializedUser.Stats.BonusExtraXp,
            $"BonusExtraXp should be {Fixture.TestUser.Stats.BonusExtraXp}, is {deserializedUser.Stats.BonusExtraXp}");
        Assert.True(Fixture.TestUser.Stats.BaseExtraXp == deserializedUser.Stats.BaseExtraXp,
            $"BaseExtraXp should be {Fixture.TestUser.Stats.BaseExtraXp}, is {deserializedUser.Stats.BaseExtraXp}");
        Assert.True(Fixture.TestUser.Stats.BonusExtraItemFind == deserializedUser.Stats.BonusExtraItemFind,
            $"BonusExtraItemFind should be {Fixture.TestUser.Stats.BonusExtraItemFind}, is {deserializedUser.Stats.BonusExtraItemFind}");
        Assert.True(Fixture.TestUser.Stats.BaseExtraItemFind == deserializedUser.Stats.BaseExtraItemFind,
            $"BaseExtraItemFind should be {Fixture.TestUser.Stats.BaseExtraItemFind}, is {deserializedUser.Stats.BaseExtraItemFind}");
        Assert.True(Fixture.TestUser.Stats.BonusLifeSteal == deserializedUser.Stats.BonusLifeSteal,
            $"BonusLifeSteal should be {Fixture.TestUser.Stats.BonusLifeSteal}, is {deserializedUser.Stats.BonusLifeSteal}");
        Assert.True(Fixture.TestUser.Stats.BaseLifeSteal == deserializedUser.Stats.BaseLifeSteal,
            $"BaseLifeSteal should be {Fixture.TestUser.Stats.BaseLifeSteal}, is {deserializedUser.Stats.BaseLifeSteal}");
        Assert.True(Fixture.TestUser.Stats.BonusManaSteal == deserializedUser.Stats.BonusManaSteal,
            $"BonusManaSteal should be {Fixture.TestUser.Stats.BonusManaSteal}, is {deserializedUser.Stats.BonusManaSteal}");
        Assert.True(Fixture.TestUser.Stats.BaseManaSteal == deserializedUser.Stats.BaseManaSteal,
            $"BaseManaSteal should be {Fixture.TestUser.Stats.BaseManaSteal}, is {deserializedUser.Stats.BaseManaSteal}");
        Assert.True(Fixture.TestUser.Stats.BonusInboundDamageToMp == deserializedUser.Stats.BonusInboundDamageToMp,
            $"BonusInboundDamageToMp should be {Fixture.TestUser.Stats.BonusInboundDamageToMp}, is {deserializedUser.Stats.BonusInboundDamageToMp}");
        Assert.True(Fixture.TestUser.Stats.BaseInboundDamageToMp == deserializedUser.Stats.BaseInboundDamageToMp,
            $"BaseInboundDamageToMp should be {Fixture.TestUser.Stats.BaseInboundDamageToMp}, is {deserializedUser.Stats.BaseInboundDamageToMp}");
    }
}