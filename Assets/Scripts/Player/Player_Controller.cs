using System.Diagnostics;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using JKFrame;
using TMPro;
using Unity.VisualScripting;
using Debug = UnityEngine.Debug;
using StateMachine = JKFrame.StateMachine;

public enum PlayerState
{
    Idle,
    Move,
    Attack,
    Hurt,
    Dead
}


public class Player_Controller : SingletonMono<Player_Controller>, IStateMachineOwner {
    [SerializeField] public Player_Model playerModel;
    [SerializeField] Animator animator;
    [SerializeField] private Collider closeCollider;
    public Collider CloseCollider { get => closeCollider; }
    public CharacterController characterController;
    private StateMachine stateMachine;
    private PlayerConfig playerConfig;
    public float rotateSpeed { get => playerConfig.rotateSpeed; }
    public float moveSpeed { get => playerConfig.moveSpeed; }
    public Transform playerTransform { get; private set; }      // 玩家位置/转向数据信息
    public Vector2 positionXScope { get; private set; }         // 相机能移动的X轴范围
    public Vector2 positionZScope { get; private set; }         // 相机能移动的Y轴范围
    public bool canUseItem { get; private set; } = true;        // 玩家当前是否能使用物品
    public bool canPickUpItem { get; private set; } = true;     // 玩家当前能否捡起物品

    #region 存档相关数据

    private PlayerTransformData playerTransformData;
    private PlayerMainData playerMainData;

    #endregion

    #region 初始化信息

    public void Init(float mapSizeOnWorld) {
        // 确定角色配置
        playerConfig = ConfigManager.Instance.GetConfig<PlayerConfig>(ConfigName.Player);

        // 确定存档位置
        playerTransformData = ArchiveManager.Instance.playerTransformData;
        playerMainData = ArchiveManager.Instance.playerMainData;

        // 初始化模型使用的一些事件
        playerModel.Init(
            PlayAudioOnFootstep, OnStartHit, OnStopHit, OnAttackOver,
            OnHurtOver, OnDeadOver
        );
        
        // 初始化状态机
        stateMachine = ResManager.Load<StateMachine>();
        // stateMachine = PoolManager.Instance.GetObject<StateMachine>();
        stateMachine.Init(this);
        // 设置初始状态: 待机
        ChangeState(PlayerState.Idle);
        InitPositionScope(mapSizeOnWorld);

        // 初始化角色位置相关数据: 玩家坐标、旋转、缩放
        playerTransform = transform;
        playerTransform.localPosition = playerTransformData.position;
        playerTransform.localRotation = Quaternion.Euler(playerTransformData.rotation);

        // 触发角色数据初始化事件改变UI填充比例
        TriggerUpdateHPEvent();
        TriggerUpdateHungryEvent();
        
        EventManager.AddEventListener(EventName.SaveGame, OnGameSave);
    }

    // 传入游戏内3D地图大小初始化相机移动范围, 需要注意由于有Y轴高度, 所以相机移动
    // 范围需要适当的缩小, 可通过提前在scene中测量得到合适的值
    private void InitPositionScope(float mapSizeOnWorld) {
        positionXScope = new Vector2(1, mapSizeOnWorld - 1);
        positionZScope = new Vector2(1, mapSizeOnWorld - 1);
    }

    #endregion


    #region 核心数值

    // 计算当前角色饱食度
    private void CalculateHungryOnUpdate() {
        if (playerMainData.hungry > 0) {
            playerMainData.hungry -= Time.deltaTime * playerConfig.hungryReduceSpeed;
            playerMainData.hungry = playerMainData.hungry > 0 ? playerMainData.hungry : 0;
            TriggerUpdateHungryEvent();
        }
        else {
            if (playerMainData.hp > 0) {
                playerMainData.hp -= Time.deltaTime * playerConfig.hpReduceSpeedOnHungryIsZero;
                playerMainData.hp = playerMainData.hp > 0 ? playerMainData.hp : 0;
                TriggerUpdateHPEvent();
            }
            else {
                playerMainData.hp = 0;
                TriggerUpdateHPEvent();
                ChangeState(PlayerState.Dead);
                // UIManager.Instance.AddTips("玩家死亡");
            }
        }
    }

    // 恢复生命值
    public void RecoverHP(float value) {
        playerMainData.hp = Mathf.Clamp(playerMainData.hp + value, 0, playerConfig.maxHP);
        TriggerUpdateHPEvent();
    }

    // 恢复饱食度
    public void RecoverHungry(float value) {
        playerMainData.hungry = Mathf.Clamp(playerMainData.hungry + value, 0, playerConfig.maxHungry);
        TriggerUpdateHungryEvent();
    }

    // 触发更新生命值事件, 当生命值发生变动时需要触发更新事件
    private void TriggerUpdateHPEvent() {
        EventManager.EventTrigger(EventName.UpdatePlayerHP, playerMainData.hp);
    }

    // 触发更新饱食度事件, 当饱食度发生变动时需要触发更新事件
    private void TriggerUpdateHungryEvent() {
        EventManager.EventTrigger(EventName.UpdatePlayerHungry, playerMainData.hungry);
    }

    #endregion

    #region 武器相关

    private ItemData currentWeaponItemData; // 当前武器数据
    private GameObject currentWeaponGameObject; // 当前武器模型

    // 修改武器: 武器数值、动画、图标等
    public void ChangeWeapon(ItemData newWeapon) {
        // 如果没有切换武器
        if (currentWeaponItemData == newWeapon) {
            return;
        }

        // 旧武器如果有数据, 则需要放回对象池进行回收
        if (currentWeaponItemData != null) {
            currentWeaponGameObject.JKGameObjectPushPool(); // 放进对象池时是基于GameObject.name的, 因此不能重名
        }

        // 新武器如果!=null则需要更新武器模型, 否则角色应该切换为空手状态
        currentWeaponItemData = newWeapon;
        if (newWeapon != null) {
            ItemWeaponInfo itemWeaponInfo = newWeapon.config.itemTypeInfo as ItemWeaponInfo;
            // 设置新武器模型: 武器位置、角度、动画
            currentWeaponGameObject = PoolManager.Instance.GetGameObject(itemWeaponInfo.prefabOnPlayer, playerModel.weaponRoot);
            currentWeaponGameObject.transform.localPosition = itemWeaponInfo.positionOnPlayer;
            currentWeaponGameObject.transform.localRotation = Quaternion.Euler(itemWeaponInfo.rotationOnPlayer);
            animator.runtimeAnimatorController = itemWeaponInfo.animatorController;
            // 需要重新激活一次动画, 动画会出错, 例如在移动中突然切换AnimatorController会不播放动画
            ChangeState(PlayerState.Idle);
        }
        else {
            animator.runtimeAnimatorController = playerConfig.normalAnimatorController;
        }
        ChangeState(PlayerState.Idle);
    }

    #endregion


    #region 战斗/伐木/采摘

    private bool canAttack = true;  // 玩家当前能否攻击
    // private bool canMove = true;    // 玩家当前是否能移动
    public Quaternion attackDirection { get; private set; } // 当前攻击方向
    private List<MapObjectBase> lastAttackMapObjectList = new List<MapObjectBase>();
    private int attackSucceedCount = 0; // 攻击时成功命中地图对象数量
    private MapObjectBase lastHitMapObject = null; // 最后攻击地图对象

    // 选择地图对象或AI时
    public void OnSelectMapObject(RaycastHit hitInfo, bool isMouseButtonDown) {
        if (hitInfo.collider.TryGetComponent<MapObjectBase>(out MapObjectBase mapObject)) {
            // 检查地图对象触碰距离是否合法
            float dis = Vector3.Distance(playerTransform.position, mapObject.transform.position);
            if (mapObject.TouchDistance < 0 || mapObject.TouchDistance < dis) {
                if (isMouseButtonDown == false) {
                    return;
                }
                UIManager.Instance.AddTips("距离目标太远.");
                ProjectTool.PlayerAudio(AudioType.Fail);
                return;
            }
            // 判断当前地图对象是否可以拾取
            if (mapObject.CanPickUp) {
                if (isMouseButtonDown == false || canPickUpItem == false) {
                    return;
                }
                lastHitMapObject = null;
                int pickUpItemConfigId = mapObject.PickUpItemConfigId;
                if (pickUpItemConfigId == -1) {
                    return;
                }
                // 将物品放回物品快捷栏, 注意如果背包满了则不应该将该物品放入背包
                if (InventoryManager.Instance.AddMainInventoryWindowItem(pickUpItemConfigId)) {
                    // 拾取物品并销毁地图对象
                    mapObject.OnPickUp();
                    // 播放动画, 需要注意当前并未切换状态, 仅在执行动画
                    PlayAnimation("PickUp");
                    // 播放成功音效
                    ProjectTool.PlayerAudio(AudioType.Bag);
                }
                else {
                    if (isMouseButtonDown) {
                        // 显示系统提示
                        UIManager.Instance.AddTips("背包已满.");
                        // 显示失败音效
                        ProjectTool.PlayerAudio(AudioType.Fail);
                    }
                }
                return;
            }

            // 判断攻击并根据玩家选中的地图对象类型以及当前角色的武器来判断做什么
            if (canAttack == true) {
                // 如果现在交互的对象不是最后攻击对象则返回, 主要解决采摘后在进行砍伐时出现的bug
                if (lastHitMapObject != mapObject && isMouseButtonDown == false) {
                    return;
                }
                switch (mapObject.ObjectType) {
                    case mapObjectType.Tree:
                        if (!CheckHitMapObject(mapObject, WeaponType.Axe) && isMouseButtonDown) {
                            UIManager.Instance.AddTips("只有石斧能砍树.");
                            ProjectTool.PlayerAudio(AudioType.Fail);
                        }

                        break;
                    case mapObjectType.Stone:
                        if (!CheckHitMapObject(mapObject, WeaponType.PickAxe) && isMouseButtonDown) {
                            UIManager.Instance.AddTips("只有铁镐能采石.");
                            ProjectTool.PlayerAudio(AudioType.Fail);
                        }

                        break;
                    case mapObjectType.Bush:
                        if (!CheckHitMapObject(mapObject, WeaponType.Sickle) && isMouseButtonDown) {
                            UIManager.Instance.AddTips("只有镰刀能收集灌木.");
                            ProjectTool.PlayerAudio(AudioType.Fail);
                        }

                        break;
                    default:
                        break;
                }

                return;
            }
        }

        // 检查是否攻击AI物体
        if (canAttack == true && currentWeaponItemData != null &&
            hitInfo.collider.TryGetComponent<AIBase>(out AIBase aiObject)) {
            // 检查地图对象触碰距离是否合法
            float dis = Vector3.Distance(playerTransform.position, aiObject.transform.position);
            // 交互距离: 武器长度 + AI半径
            ItemWeaponInfo itemWeaponInfo = (currentWeaponItemData.config.itemTypeInfo as ItemWeaponInfo);
            if (dis <= itemWeaponInfo.attackDistance + aiObject.Radius) {
                // 计算方向
                attackDirection = Quaternion.LookRotation(aiObject.transform.position - transform.position);
                // 播放音效
                AudioManager.Instance.PlayOnShot(itemWeaponInfo.attackAudio, transform.position, 0.5f);
                // 切换状态
                ChangeState(PlayerState.Attack);
                // 记录最后一个攻击对象
                lastHitMapObject = mapObject;
            }
        }
    }

    // 玩家受伤函数
    public void Hurt(float damage) {
        // 更新玩家血量
        if (playerMainData.hp <= 0) {
            return;
        }

        playerMainData.hp -= damage;
        if (playerMainData.hp <= 0) {
            playerMainData.hp = 0;
            TriggerUpdateHPEvent();
            ChangeState(PlayerState.Dead);
        }
        else {
            // 更新UI
            TriggerUpdateHPEvent();
            ChangeState(PlayerState.Hurt);
        }
    }

    // 开启攻击: 开启伤害检测
    private void OnStartHit() {
        // 清空标记对象数量
        attackSucceedCount = 0;
        currentWeaponGameObject.transform.OnTriggerEnter(OnWeaponTriggerEnter);
    }

    // 停止攻击: 停止伤害检测
    public void OnStopHit() {
        // 清空攻击标记数组
        lastAttackMapObjectList.Clear();
        currentWeaponGameObject.transform.RemoveTriggerEnter(OnWeaponTriggerEnter);
    }
    
    // 攻击动作结束
    private void OnAttackOver() {
        // 更新武器耐久度
        for (int i = 0; i < attackSucceedCount; i++) {
            EventManager.EventTrigger(EventName.PlayerWeaponAttackSucceed);
        }
        // 切换状态到待机
        ChangeState(PlayerState.Idle);
    }

    // 受伤动作结束
    private void OnHurtOver() {
        ChangeState(PlayerState.Idle);
    }

    // 死亡动作结束
    private void OnDeadOver() {
        // 整个游戏结束
        GameSceneManager.Instance.PlayerDeadGameOver();
    }

    // 武器触发器: 当武器碰到物体(地图对象/AI)时
    private void OnWeaponTriggerEnter(Collider other, object[] arg2) {
        // 判断对方是否为地图对象
        if (other.TryGetComponent<MapObjectBase>(out MapObjectBase mapObject)) {
            // 记录攻击对象, 防止计算二次伤害, 该数组中包含的地图对象不一定能成功攻击
            // 例如无法用石斧攻击矿石
            if (lastAttackMapObjectList.Contains(mapObject)) return;
            lastAttackMapObjectList.Add(mapObject);
            // 检测对方类型以及当前武器类型
            switch (mapObject.ObjectType) {
                case mapObjectType.Tree:
                    // 判断当前武器是否为石斧
                    CheckMapObjectHurt(mapObject as HitMapObjectBase, WeaponType.Axe);
                    break;
                case mapObjectType.Stone:
                    // 判断当前武器是否为铁镐
                    CheckMapObjectHurt(mapObject as HitMapObjectBase, WeaponType.PickAxe);
                    break;
                case mapObjectType.Bush:
                    // 判断当前武器是否为铁镐
                    CheckMapObjectHurt(mapObject as HitMapObjectBase, WeaponType.Sickle);
                    break;
                default:
                    break;
            }
        }
        else if (other.TryGetComponent<AIBase>(out AIBase AIObject)) {
            ItemWeaponInfo itemWeaponInfo = (currentWeaponItemData.config.itemTypeInfo as ItemWeaponInfo);
            // 显示打击效果
            GameObject effect = PoolManager.Instance.GetGameObject(itemWeaponInfo.hitEffect);
            effect.transform.position =
                other.ClosestPoint(currentWeaponGameObject.transform.position); // ClosestPoint: 返回一个碰撞体最近传入参数点的位置
            // 播放音效
            AudioManager.Instance.PlayOnShot(itemWeaponInfo.hitAudio, transform.position, 0.5f);
            AIObject.Hurt(itemWeaponInfo.attackValue);
            attackSucceedCount += 1;
        }
    }

    // 检测地图对象能否受伤
    private void CheckMapObjectHurt(HitMapObjectBase hitMapObject, WeaponType weaponType) {
        ItemWeaponInfo itemWeaponInfo = (currentWeaponItemData.config.itemTypeInfo as ItemWeaponInfo);
        if (itemWeaponInfo.weaponType == weaponType) {
            // 播放音效
            AudioManager.Instance.PlayOnShot(itemWeaponInfo.hitAudio, transform.position, 0.5f);
            hitMapObject.Hurt(itemWeaponInfo.attackValue);
            attackSucceedCount += 1;
        }
    }

    // 检查是否可以攻击当前地图对象
    private bool CheckHitMapObject(MapObjectBase mapObject, WeaponType weaponType) {
        // 如果能攻击并且武器类型符合要求
        if (canAttack &&
            currentWeaponItemData != null &&
            (currentWeaponItemData.config.itemTypeInfo as ItemWeaponInfo).weaponType == weaponType) {
            ItemWeaponInfo itemWeaponInfo = (currentWeaponItemData.config.itemTypeInfo as ItemWeaponInfo);
            // 计算方向
            attackDirection = Quaternion.LookRotation(mapObject.transform.position - transform.position);
            // 播放音效
            AudioManager.Instance.PlayOnShot(itemWeaponInfo.attackAudio, transform.position, 0.5f);
            // 切换状态
            ChangeState(PlayerState.Attack);
            // 记录最后一个攻击对象
            lastHitMapObject = mapObject;
            return true;
        }

        return false;
    }

    #endregion


    #region 辅助函数: e.g. 状态变化, 播放音效

    // 修改状态
    public void ChangeState(PlayerState playerState) {
        switch (playerState) {
            case PlayerState.Idle:
                canAttack = true;
                canUseItem = true;
                canPickUpItem = true;
                bool res = stateMachine.ChangeState<Player_Idle>((int)playerState);
                break;
            case PlayerState.Move:
                canAttack = true;
                canUseItem = true;
                canPickUpItem = false;
                stateMachine.ChangeState<Player_Move>((int)playerState);
                break;
            case PlayerState.Attack:
                canAttack = false;
                canUseItem = false;
                canPickUpItem = false;
                stateMachine.ChangeState<Player_Attack>((int)playerState);
                break;
            case PlayerState.Hurt:
                canAttack = false;
                canUseItem = true;
                canPickUpItem = false;
                stateMachine.ChangeState<Player_Hurt>((int)playerState);
                break;
            case PlayerState.Dead:
                canAttack = false;
                canUseItem = false;
                canPickUpItem = false;
                stateMachine.ChangeState<Player_Dead>((int)playerState, false);
                break;
        }
    }

    private void PlayAudioOnFootstep(int index) {
        AudioManager.Instance.PlayOnShot(
            playerConfig.footstepAudioClips[index], playerTransform.position,
            playerConfig.footstepVolume
        );
    }

    // 播放动画
    public void PlayAnimation(string animationName, float fixedTime = 0.25f) {
        animator.CrossFadeInFixedTime(animationName, fixedTime);
    }

    #endregion

    private void Update() {
        if (GameSceneManager.Instance.IsInitialized == false) return;
        CalculateHungryOnUpdate();
    }

    // 场景切换或关闭时将存档数据写入磁盘
    private void OnGameSave() {
        playerTransformData.position = playerTransform.localPosition;
        playerTransformData.rotation = playerTransform.localRotation.eulerAngles;
        ArchiveManager.Instance.SavePlayerTransformData();
        ArchiveManager.Instance.SavePlayerMainData();
    }

    private void OnDestroy() {
        // 销毁状态机, 状态机不依赖于场景因此需要主动销毁
        if (stateMachine != null) {
            stateMachine.Destory();
            stateMachine = null;
        }
    }
}