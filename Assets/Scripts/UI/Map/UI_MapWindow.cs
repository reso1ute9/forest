using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using JKFrame;

// UI地图窗口
// 使用JKFrame中的特性设置缓存/资源路径/UI层数
[UIElement(true, "UI/UI_MapWindow", 4)]
public class UI_MapWindow : UI_WindowBase
{
    [SerializeField] private RectTransform content;     // 所有地图块/icon显示的父物体
    private float contentSize;                          // 地图尺寸
    [SerializeField] private GameObject mapItemPrefab;  // 单个地图块在UI中的预制体
    [SerializeField] private GameObject mapIconPrefab;  // 单个icon在UI中的预制体
    [SerializeField] private RectTransform playerIcon;  // 玩家所在位置的icon

    private Dictionary<Vector2Int, Image> mapImageDict = new Dictionary<Vector2Int, Image>();   // 地图图片字典
    private int mapChunkSize;           // 一个地图块有多少格子
    private float mapChunkImageSize;    // UI地图块图片的尺寸
    private float mapSizeOnWorld;       // 游戏内3D地图大小
    private Sprite forestSprite;        // 森林地块的精灵

    private float minScale;             // 地图最小放大倍数
    private float maxScale = 10;        // 地图最大放大倍数
    private float mapScaleFactorNum = 10;   // 预设值: UI地图content与原始地图的比例系数


    public override void Init()
    {
        // 当修改值时与修改值重合
        transform.Find("Scroll View").GetComponent<ScrollRect>().onValueChanged.AddListener(UpdatePlayerIconPosition);
    }

    public void Update() {
        // 得到鼠标滚轮滚动数值
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0) {
            float newScale = Mathf.Clamp(content.localScale.x + scroll, minScale, maxScale);
            content.localScale = new Vector3(newScale, newScale, 0);
        }
    }

    // 初始化地图
    // mapSize: 一行或者一列有多少个image/chunk
    // mapSizeOnWorld: 地图在世界中一行或者一列有多大
    // forestTexture: 森林贴图
    public void InitMap(float mapSize, int mapChunkSize, float mapSizeOnWorld,Texture2D forestTexture) {
        this.mapChunkSize = mapChunkSize;
        this.mapSizeOnWorld = mapSizeOnWorld;
        this.forestSprite = CreateMapSprite(forestTexture);

        // content尺寸: 默认content尺寸要大于地图尺寸
        contentSize = mapSizeOnWorld * mapScaleFactorNum;
        this.content.sizeDelta = new Vector2(contentSize, contentSize);
        Debug.Log("contentSize:" + contentSize);

        // 一个UI地图块尺寸
        mapChunkImageSize = contentSize / mapSize;
        minScale = 1050f / contentSize;
    }

    // 更新中心点, 保证鼠标缩放的时候中心点是玩家
    public void UpdatePivot(Vector3 viewerPosition) {
        float x = viewerPosition.x / mapSizeOnWorld;
        float y = viewerPosition.z / mapSizeOnWorld;
        // 修改content后会导致Scroll Rect组件的当值修改时间=>UpdatePlayerIconPosition
        content.pivot = new Vector2(x, y);
    }

    public void UpdatePlayerIconPosition(Vector2 value) {
        // 玩家icon完全放在content中心点
        playerIcon.anchoredPosition3D = content.anchoredPosition3D;
    }

    // 生成地图块的Sprite 
    private Sprite CreateMapSprite(Texture2D texture) {
        return Sprite.Create(
            texture, new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f)
        );
    }

    // 添加地图块UI
    public void AddMapChunk(Vector2Int chunkIndex, List<MapChunkMapObjectModel> mapObjectList, Texture2D texture = null) {
        RectTransform mapChunkRect = Instantiate(mapItemPrefab, content).GetComponent<RectTransform>();
        // 确定地图块在UI界面中的位置和大小(宽高)
        mapChunkRect.anchoredPosition = new Vector2(chunkIndex.x * mapChunkImageSize, chunkIndex.y * mapChunkImageSize);
        mapChunkRect.sizeDelta = new Vector2(mapChunkImageSize, mapChunkImageSize);
        // 设置贴图
        Image mapChunkImage = mapChunkRect.GetComponent<Image>();
        // 森林贴图
        if (texture == null) {
            mapChunkImage.type = Image.Type.Tiled;
            // 设置贴瓷砖的比例, 在一个image中显示这个地图块所包含的格子数量
            // 计算森林贴图与当前地图块贴图尺寸比例
            float ratio = forestSprite.texture.width / mapChunkImageSize;
            mapChunkImage.pixelsPerUnitMultiplier = mapChunkSize * ratio;
            mapChunkImage.sprite = forestSprite;
        } else {
            mapChunkImage.sprite = CreateMapSprite(texture);
        }
        // TODO: 添加物体icon
        for (int i = 0; i < mapObjectList.Count; i++) {
            MapObjectConfig config = ConfigManager.Instance.GetConfig<MapObjectConfig>(ConfigName.mapObject, mapObjectList[i].configId);
            // 按照id一定能查到物体, 但是物体不一定具有地图icon
            if (config.mapIconSprite == null) {
                continue;
            }
            GameObject tempObject = PoolManager.Instance.GetGameObject(mapIconPrefab, content);
            tempObject.GetComponent<Image>().sprite = config.mapIconSprite;
            // 因为整个content的尺寸在初始化的时候已经乘上mapScaleFactorNum了, 所以在计算icon位置时
            // 也需要乘上相同的系数
            float x = mapObjectList[i].position.x * mapScaleFactorNum;
            float y = mapObjectList[i].position.z * mapScaleFactorNum;
            tempObject.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, y);

        }

        // TODO: 待重构, 后续需要保存icon信息, icon信息可能会移除(树、花被销毁)
        mapImageDict.Add(chunkIndex, mapChunkImage);
    }
}
