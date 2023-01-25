using System;
using System.Collections.Generic;
using System.Linq;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Utilities;

namespace Cyrillya;

public class MinionBossBag : ModItem
{
    public override void SetStaticDefaults() {
        ItemID.Sets.BossBag[Type] = true;
        ItemID.Sets.PreHardmodeLikeBossBag[Type] = true;
    }

    public override void SetDefaults() {
        Item.maxStack = 999;
        Item.consumable = true;
        Item.width = 24;
        Item.height = 24;
    }

    public override bool CanRightClick() {
        return true;
    }

    public override void ModifyItemLoot(ItemLoot itemLoot) {
        // 生成带权重的规则并添加到掉落池
        itemLoot.Add(new OneFromOptionsByWeight(
            (new ItemDropInfo(ItemID.GoldCoin, 3), 0.7),
            (new ItemDropInfo(ItemID.GelBalloon, 2), 0.2),
            (new ItemDropInfo(ItemID.RedRocket), 0.025),
            (new ItemDropInfo(ItemID.BlueRocket), 0.025),
            (new ItemDropInfo(ItemID.GreenRocket), 0.025),
            (new ItemDropInfo(ItemID.YellowRocket), 0.025)
        ));
    }
}

/// <summary>
/// 集合了物品掉落基础信息的类
/// </summary>
public struct ItemDropInfo
{
    public int ItemID;
    public int StackMinimum = 1;
    public int StackMaximum = 1;

    public ItemDropInfo(int itemID) {
        ItemID = itemID;
    }

    public ItemDropInfo(int itemID, int stackMaximum) {
        ItemID = itemID;
        StackMaximum = stackMaximum;
    }

    public ItemDropInfo(int itemID, int stackMinimum, int stackMaximum) {
        ItemID = itemID;
        StackMinimum = stackMinimum;
        StackMaximum = stackMaximum;
    }
}

/// <summary>
/// 从一堆 Rule 中随机选一个
/// </summary>
public class OneFromOptionsByWeight : IItemDropRule
{
    /// <summary> 含权重的掉率列表 </summary>
    private readonly (ItemDropInfo, double)[] _itemDropInfoWithWeight;

    // 下面这三个值不添加到构造函数了，因为我觉得没必要，如果有需要的话自己加，或者构造后修改

    /// <summary> 分母 </summary>
    public int ChanceDenominator = 1;

    /// <summary> 分子 </summary>
    public int ChanceNumerator = 1;

    /// <summary> 在进行是否会掉落判断时，几率是否受玩家幸运影响 </summary>
    public bool ScalingWithLuck;

    public List<IItemDropRuleChainAttempt> ChainedRules { get; }

    public OneFromOptionsByWeight(params (ItemDropInfo, double)[] itemInfoTuples) {
        _itemDropInfoWithWeight = itemInfoTuples;
        ChainedRules = new List<IItemDropRuleChainAttempt>();
    }

    public bool CanDrop(DropAttemptInfo info) => true;

    public ItemDropAttemptResult TryDroppingItem(DropAttemptInfo info) {
        bool canDrop = ScalingWithLuck
            ? info.player.RollLuck(ChanceDenominator) < ChanceNumerator
            : info.rng.Next(ChanceDenominator) < ChanceNumerator;

        if (!canDrop) return new ItemDropAttemptResult {State = ItemDropAttemptResultState.FailedRandomRoll};

        // 以 _itemDropInfosWithWeight 生成一个 WeightedRandom<ItemDropInfo>
        // 之所以在这里才生成，是因为需要用到 info.rng，这在实例化的时候无法获取
        var tupleArray = _itemDropInfoWithWeight.Select(valueTuple => valueTuple.ToTuple()).ToArray();
        var weightedRandom = new WeightedRandom<ItemDropInfo>(info.rng, tupleArray);
        var result = weightedRandom.Get();

        // 进行掉落
        CommonCode.DropItem(info, result.ItemID, info.rng.Next(result.StackMinimum, result.StackMaximum + 1));

        return new ItemDropAttemptResult {State = ItemDropAttemptResultState.Success};
    }

    // 这里是调用所有内在 Rule 的 ReportDroprates 方法
    // 也就是说，报告的掉落率与 OnlyOneSuccessDropRule 无关，而是其内在的规则决定的
    // 
    public void ReportDroprates(List<DropRateInfo> drops, DropRateInfoChainFeed ratesInfo) {
        // 计算总权重
        double totalWeight = _itemDropInfoWithWeight.Sum(x => x.Item2);

        // 计算这个 Rule 本身的掉率
        float rulePersonalDropRate = ChanceNumerator / (float) ChanceDenominator;

        // 遍历
        foreach ((var itemDropInfo, double weight) in _itemDropInfoWithWeight) {
            // 计算权重比例，得出个体掉率
            float itemPersonalDropRate = (float) (weight / totalWeight);
            // 乘上本体规则与父规则掉率，得出最终掉率
            float finalDropRate = itemPersonalDropRate * rulePersonalDropRate * ratesInfo.parentDroprateChance;

            // 生成报告
            drops.Add(new DropRateInfo(itemDropInfo.ItemID, itemDropInfo.StackMinimum, itemDropInfo.StackMaximum,
                finalDropRate, ratesInfo.conditions));
        }

        // 对其子规则进行报告
        Chains.ReportDroprates(ChainedRules, rulePersonalDropRate, drops, ratesInfo);
    }
}
