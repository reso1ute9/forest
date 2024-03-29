using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class Player_Model : MonoBehaviour {
    [SerializeField] public Transform weaponRoot;   // 武器模型节点
    private Action<int> footstepAction;
    private Action startHitAction;
    private Action stopHitAction;
    private Action attackOverAction;
    private Action hurtOver;
    private Action deadOver;

    public void Init(
        Action<int> footstepAction, 
        Action startHitAction,
        Action stopHitAction,
        Action attackOverAction,
        Action hurtOver, 
        Action deadOver
        ) {
        this.footstepAction = footstepAction;
        this.startHitAction = startHitAction;
        this.stopHitAction = stopHitAction;
        this.attackOverAction = attackOverAction;
        this.hurtOver = hurtOver;
        this.deadOver = deadOver;
    }

    #region 动画事件
    // 脚步
    private void Footstep(int index) {
        footstepAction?.Invoke(index);
    }

    // 攻击开始动作
    private void StartHit() {
        startHitAction?.Invoke();
    }

    // 攻击结束动作
    private void StopHit() {
        stopHitAction?.Invoke();
    }

    // 攻击结束
    private void AttackOver() {
        attackOverAction?.Invoke();
    }
    
    // 玩家受伤
    private void HurtOver() {
        hurtOver?.Invoke();
    }
    
    // 玩家死亡
    private void DeadOver() {
        deadOver?.Invoke();
    }
    #endregion
}
