using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using JKFrame;

// 物品快捷栏中的格子
public class UI_ItemSlot : MonoBehaviour
{
    [SerializeField] public Image bgImg;                   // 格子背景图片
    [SerializeField] Image iconImg;                 // 格子里图标
    [SerializeField] Text countText;                // 格子中显示的数值
    
    public ItemData itemData { get; private set; }  // 格子中的数据
    public int index { get; private set; }          // 格子编号

    private UI_InventoryWindow ownerWindow;         // 宿主窗口: 物品栏/仓库

    private Transform iconTransform;
    private Transform slotTransform;                // 保存当前格子的父物体

    public static UI_ItemSlot currentMouseEnterSlot;    // 当前鼠标进入/出入的格子
    public static UI_ItemSlot weaponSlot;               // 记录一下当前的武器栏
    public static List<RaycastResult> raycastResults = new List<RaycastResult>(10); // 记录鼠标与UI碰撞的结果
    private static Sprite weaponSlotDefaultSprite;      // 记录默认的武器栏图标

    private void Start() {
        iconTransform = iconImg.transform;
        slotTransform = transform;
        // 鼠标交互事件
        this.OnMouseEnter(MouseEnter);
        this.OnMouseExit(MouseExit);
        this.OnBeginDrag(BeginDrag);                  // 开始拖拽
        this.OnDrag(Drag);                            // 拖拽过程中
        this.OnEndDrag(EndDrag);                      // 拖拽结束
        // 设置物品快捷栏默认背景
        if (weaponSlot != this) {
            bgImg.sprite = ownerWindow.bgSprite[0];
        }
        weaponSlotDefaultSprite = weaponSlot.bgImg.sprite;
    }

    private void OnEnable() {
        this.OnUpdate(CheckMouseRightClick);          // 检测鼠标右键是否点击
    }

    private void OnDisable() {
        this.RemoveUpdate(CheckMouseRightClick);        
    }

    // 检测鼠标右键是否可以使用物品
    private void CheckMouseRightClick() {
        if (itemData == null) return;
        if (isMouseStay && Input.GetMouseButtonDown(1)) {
            switch (itemData.config.itemType)
            {
                case ItemType.Weapon:
                    Debug.Log("可以使用" + itemData.config.itemName);
                    break;
                case ItemType.Consumable:
                    Debug.Log("可以使用" + itemData.config.itemName);
                    break;
                default:
                    Debug.Log("无法使用");
                    break;
            }
        }
    }

    // 初始化格子
    public void Init(int index, UI_InventoryWindow ownerWindow) {
        this.index = index;
        this.ownerWindow = ownerWindow;
    }

    // 初始化格子中的数据
    public void InitData(ItemData itemData = null) {
        this.itemData = itemData;
        // 如果数据为空则是空格子
        if (itemData == null) {
            // 外框设置为白色
            bgImg.color = Color.white;
            // 隐藏格子数值
            countText.gameObject.SetActive(false);
            // 隐藏格子图标
            iconImg.sprite = null;
            iconImg.gameObject.SetActive(false);
            return;
        }
        // 有数据时需要提前打开外框、数值、图标 
        countText.gameObject.SetActive(true);
        iconImg.gameObject.SetActive(true);
        iconImg.sprite = itemData.config.itemIcon;
        // 根据不同的物品类型显示不同的效果
        switch (itemData.config.itemType)
        {
            case ItemType.Weapon:
                bgImg.color = Color.white;
                countText.text = (itemData.itemTypeData as ItemWeaponData).durability.ToString() + "%";
                break;
            case ItemType.Consumable:
                // rgb通道+alpha值
                bgImg.color = new Color(0, 1, 0, 0.5f);
                countText.text = (itemData.itemTypeData as ItemConsumableData).count.ToString();
                break;
            case ItemType.Meterial:
                bgImg.color = Color.white;
                countText.text = (itemData.itemTypeData as ItemMaterialData).count.ToString();
                break;
            default:
                break;
        }
    }

    #region 鼠标交互事件
    private bool isMouseStay = false;
    private void MouseEnter(PointerEventData arg1, object[] arg2) {
        GameManager.Instance.SetCursorState(CursorState.Handle);
        bgImg.sprite = ownerWindow.bgSprite[1];
        isMouseStay = true;
        currentMouseEnterSlot = this;
    }

    private void MouseExit(PointerEventData arg1, object[] arg2) {
        GameManager.Instance.SetCursorState(CursorState.Normal);
        if (weaponSlot != this) {
            bgImg.sprite = ownerWindow.bgSprite[0];
        } else {
            bgImg.sprite = weaponSlotDefaultSprite;
        }
        isMouseStay = false;
        currentMouseEnterSlot = null;
    }

    private void BeginDrag(PointerEventData arg1, object[] arg2) {
        // 格子中没有物体
        if (itemData == null) return;
        // 将拖拽图标设置到DragLayer上保证不会被其他UI图层覆盖
        iconTransform.SetParent(UIManager.Instance.DragLayer);
    }

    private void Drag(PointerEventData arg1, object[] arg2) {
        // 格子中没有物体
        if (itemData == null) return;
        // 拖拽过程: 1. 将事件数据arg1的位置赋值给图标位置; 2. 保持鼠标形状
        GameManager.Instance.SetCursorState(CursorState.Handle);
        iconTransform.position = arg1.position;
    }

    private void EndDrag(PointerEventData arg1, object[] arg2) {
        // 格子中没有物体
        if (itemData == null) return;
        // 当前是否拖拽结束时没有到其他格子里
        if (currentMouseEnterSlot == null) {
            // 鼠标形状恢复
            GameManager.Instance.SetCursorState(CursorState.Normal);
        }
        // 拖拽中的图标复原
        iconTransform.SetParent(slotTransform);
        iconTransform.localPosition = Vector3.zero;         // 归到中心点
        // 如果拖拽到自己原本格子中
        if (currentMouseEnterSlot == this) return;
        // 检查当前格子类型是否满足拖入要求, 例如消耗品不能拖入武器栏
        if (currentMouseEnterSlot == weaponSlot && itemData.config.itemType != ItemType.Weapon) {
            UIManager.Instance.AddTips("只能放入武器");
            return;
        } else {
            Debug.Log("可以装备物品:" + itemData.config.itemName);
        }
        // 检查拖拽结束时物品在不在格子上
        if (currentMouseEnterSlot == null) {
            // 如果目标没有格子, 但是是UI物体, 可以无视
            // if (EventSystem.current.IsPointerOverGameObject()) return;
            // 使用射线去检测是否放到了地面上
            EventSystem.current.RaycastAll(arg1, raycastResults);
            for (int i = 0; i < raycastResults.Count; i++) {
                RaycastResult raycastResult = raycastResults[i];
                // 如果是UI但是不是Mask模块则返回
                if (raycastResult.gameObject.name != "Mask" && 
                    raycastResult.gameObject.TryGetComponent<RectTransform>(out var _temp)) {
                    raycastResults.Clear();
                    return;
                }
            }
            raycastResults.Clear();
            // 从存档里去除这份数据
            // TOOD: 物品掉落在地上
            // TODO: 将数据传递给地图块, 可能存在销毁的情况
            Debug.Log("物品掉落在地上" + itemData.config.itemName);
            // 物品移走时需要重新初始化当前格子
            InitData(null);
        } else {
            // 交换或放入格子
            ItemData dragItem = itemData;
            ItemData targetItem = currentMouseEnterSlot.itemData;
            // 更改格子内部数据
            InitData(targetItem);
            currentMouseEnterSlot.InitData(dragItem);
            // 注意: 有可能是跨窗口, 比如快捷栏和仓库
            // 更改物品快捷栏UI内容
            ownerWindow.SetItem(index, targetItem);
            currentMouseEnterSlot.ownerWindow.SetItem(currentMouseEnterSlot.index, dragItem);
        }
        // 更新存档
        ArchiveManager.Instance.SaveInventoryData();
    }
    #endregion 
}