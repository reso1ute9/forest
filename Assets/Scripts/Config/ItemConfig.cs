using System.Reflection.Emit;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using JKFrame;
using Sirenix.OdinInspector;
using System;

// 物品类型
public enum ItemType {
    [LabelText("装备")] Weapon,
    [LabelText("消耗品")] Consumable,
    [LabelText("材料")] Meterial,
}

// 物品配置
[CreateAssetMenu(menuName = "Config/物品配置")]
public class ItemConfig : ConfigBase
{
    [LabelText("类型"), OnValueChanged(nameof(OnItemTypeChanged))] public ItemType itemType;
    [LabelText("名称")] public string itemName; 
    [LabelText("描述"), MultiLineProperty] public string descript;
    [LabelText("图标")] public Sprite Icon;
    [LabelText("类型专属信息")] public IItemTypeInfo itemTypeInfo;

    // 当类型修改时自动生成同等类型应有的专属信息
    private void OnItemTypeChanged() {
        switch (itemType) {
            case ItemType.Weapon:
                itemTypeInfo = new ItemWeaponInfo();
                break;
            case ItemType.Consumable:
                itemTypeInfo = new ItemConsumableInfo();
                break;
            case ItemType.Meterial:
                itemTypeInfo = new ItemMeterialInfo();
                break;
            default:
                break;
        }
    }
}

// 物品类型信息结构
public interface IItemTypeInfo {}

// 武器类型信息
public class ItemWeaponInfo: IItemTypeInfo {
    [LabelText("攻击力")] public float attackValue;
}

// 消耗品类型信息
public class ItemConsumableInfo: IItemTypeInfo {
    [LabelText("堆积上限")] public int maxCount;
}

// 材料类型信息
public class ItemMeterialInfo: IItemTypeInfo {
    [LabelText("堆积上限")] public int maxCount;
}