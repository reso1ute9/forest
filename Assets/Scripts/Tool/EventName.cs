using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class EventName
{
    // 时间事件
    public static string UpdateDayNum = "UpdateDayNum";
    public static string UpdateTimeState = "UpdateTimeState";
    public static string OnMorning = "OnMorning";
    // 角色信息事件
    public static string UpdatePlayerHP = "UpdatePlayerHP";
    public static string UpdatePlayerHungry = "UpdatePlayerHungry";
    // 玩家攻击事件
    public static string PlayerWeaponAttackSucceed = "PlayerWeaponAttackSucceed";
    // 玩家拾取事件
    public static string AddItem = "AddItem";
    // 建造事件
    public static string BuildBuilding = "BuildBuilding";
    // 保存游戏
    public static string SaveGame = "SaveGame";
}
