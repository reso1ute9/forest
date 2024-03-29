using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using JKFrame;


// 地图管理器需要调用MapGenerator去生成地图上的信息
public class MapManager : SingletonMono<MapManager>
{

    // 地图UI相关变量
    private bool mapUIInitialized = false;
    private bool isShowMaping = false;
    private List<Vector2Int> mapUIUpdateChunkIndexList = new List<Vector2Int>();        // 地图UI待更新列表
    private UI_MapWindow mapUI;

    // 运行时逻辑
    #region 运行时变量
    [SerializeField] MeshCollider meshCollider;
    private MapGenerator mapGenerator;                              // 地图生成器
    private Transform viewer;                                       // 观察者
    private Vector3 lastViewerPosition = Vector3.one * -1;          // 观察者最后的位置, 用以控制地图是否进行刷新
    public Dictionary<Vector2Int, MapChunkController> mapChunkDict; // 全部已有的地图块
    private float mapSizeOnWorld;                                   // 在世界中世界的地图尺寸
    private float chunkSizeOnWorld;                                 // 在世界中实际的地图块尺寸    
    public float UpdateVisibleChunkTime = 1.0f;                     // 地图更新最小时间
    private bool canUpdateChunk = true;                             // 地图是否能进行更新
    private List<MapChunkController> lastVisibleChunkList = new List<MapChunkController>();
    #endregion
    
    // 配置
    #region 配置
    private MapConfig mapConfig;                                    // 地图配置
    public MapConfig MapConfig { get => mapConfig; }
    private AIConfig aiConfig;                                      // AI配置
    private Dictionary<MapVertexType, List<int>> spawnMapObjectConfigDict;      // 地图配置数据: 某个类型可以生成哪些地图对象Id
    private Dictionary<MapVertexType, List<int>> spawnAIObjectConfigDict;       // AI配置数据: 某个类型可以生成哪些AI对象Id
    #endregion

    // 存档
    #region 存档
    private MapInitData mapInitData;
    private MapData mapData;
    #endregion

    public void Init() {
        StartCoroutine(DoInit());
        
        EventManager.AddEventListener(EventName.SaveGame, OnGameSave);
    }

    private IEnumerator DoInit() {
        // 确定存档
        mapInitData = ArchiveManager.Instance.mapInitData;
        mapData = ArchiveManager.Instance.mapData;

        // 确定地图对象生成配置
        Dictionary<int, ConfigBase> temp_dict = ConfigManager.Instance.GetConfigs(ConfigName.MapObject);
        spawnMapObjectConfigDict = new Dictionary<MapVertexType, List<int>>();
        spawnMapObjectConfigDict.Add(MapVertexType.Forest, new List<int>());
        spawnMapObjectConfigDict.Add(MapVertexType.Marsh, new List<int>());
        foreach (var item in temp_dict) {
            MapVertexType mapVertexType = (item.Value as MapObjectConfig).mapVertexType;
            if (mapVertexType == MapVertexType.None) {
                continue;
            }
            spawnMapObjectConfigDict[mapVertexType].Add(item.Key);
        }

        // 确定AI对象生成配置
        aiConfig = ConfigManager.Instance.GetConfig<AIConfig>(ConfigName.AI);
        temp_dict = ConfigManager.Instance.GetConfigs(ConfigName.AI);
        spawnAIObjectConfigDict = new Dictionary<MapVertexType, List<int>>();
        spawnAIObjectConfigDict.Add(MapVertexType.Forest, new List<int>());
        spawnAIObjectConfigDict.Add(MapVertexType.Marsh, new List<int>());
        foreach (var item in temp_dict) {
            MapVertexType mapVertexType = (item.Value as AIConfig).mapVertexType;
            if (mapVertexType == MapVertexType.None) {
                continue;
            }
            spawnAIObjectConfigDict[mapVertexType].Add(item.Key);
        }

        // 初始化地图生成器
        mapConfig = ConfigManager.Instance.GetConfig<MapConfig>(ConfigName.Map);
        mapGenerator = new MapGenerator(
            mapConfig, mapInitData, mapData, 
            spawnMapObjectConfigDict,
            spawnAIObjectConfigDict
        );
        mapGenerator.GenerateMapData();
        mapChunkDict = new Dictionary<Vector2Int, MapChunkController>();
        chunkSizeOnWorld = mapConfig.mapChunkSize * mapConfig.cellSize;
        mapSizeOnWorld = chunkSizeOnWorld * mapInitData.mapSize;
        
        // 生成地面碰撞体的网格
        meshCollider.sharedMesh = GenerateGroundMesh(mapSizeOnWorld, mapSizeOnWorld);
        // 烘焙导航网格
        BakeNavMesh();

        // 需要判断是否需要加载之前的地图
        int mapChunkDataCount = mapData.MapChunkIndexList.Count;
        if (mapChunkDataCount > 0) {
            // 旧存档加载状态
            GameSceneManager.Instance.SetProgressMapChunkCount(mapChunkDataCount);
            // 根据存档恢复整个地图状态
            for (int i = 0; i < mapChunkDataCount; i++) {
                Serialization_Vector2 chunkIndex = mapData.MapChunkIndexList[i];
                MapChunkData mapChunkData = ArchiveManager.Instance.GetMapChunkData(chunkIndex);
                GenerateMapChunk(chunkIndex.ConverToSVector2Init(), mapChunkData).gameObject.SetActive(false);
                // 停留五帧
                for (int f = 0; f < 2; f++) yield return null;
            }
        } else {
            // 新存档加载状态
            GameSceneManager.Instance.SetProgressMapChunkCount(GetMapChunkCountOnGameInit());
            // 当前观察者所在地图块
            Vector2Int currChunkIndex = GetMapChunkIndexByWorldPosition(viewer.position);
            // 开启需要显示的地图块
            int startX = currChunkIndex.x - mapConfig.viewDistance;
            int startY = currChunkIndex.y - mapConfig.viewDistance;
            for (int x = 0; x < 2 * mapConfig.viewDistance + 1; x++) {
                for (int y = 0; y < 2 * mapConfig.viewDistance + 1; y++) {
                    Vector2Int chunkIndex = new Vector2Int(startX + x, startY + y);
                    GenerateMapChunk(chunkIndex);
                    // 停留五帧
                    for (int f = 0; f < 2; f++) yield return null;
                }
            }
        }
        // 更新目前可见地图块
        DoUpdateVisibleChunk();
        // 显示一次MapUI做初始化, 初始化后再关闭
        ShowMapUI();
        CloseMapUI();
    }

    // 游戏初始化时获得地图块的初始数量
    private int GetMapChunkCountOnGameInit() {
        int res = 0;
        Vector2Int currChunkIndex = GetMapChunkIndexByWorldPosition(viewer.position);
        int startX = currChunkIndex.x - mapConfig.viewDistance;
        int startY = currChunkIndex.y - mapConfig.viewDistance;
        for (int x = 0; x < 2 * mapConfig.viewDistance + 1; x++) {
            for (int y = 0; y < 2 * mapConfig.viewDistance + 1; y++) {
                Vector2Int chunkIndex = new Vector2Int(startX + x, startY + y);
                if (chunkIndex.x > mapInitData.mapSize - 1 || chunkIndex.y > mapInitData.mapSize - 1) continue;
                if (chunkIndex.x < 0 || chunkIndex.y < 0) continue;
                res += 1;
            }
        }
        return res;
    }

    public void UpdateView(Transform viewer) {
        this.viewer = viewer;
    }

    private void Update() {
        // 当地图场景未加载好时不需要执行下面的操作
        if (GameSceneManager.Instance.IsInitialized == false) {
            return;
        }
        UpdateVisibleChunk();
        if (Input.GetKeyDown(KeyCode.M)) {
            if (isShowMaping) {
                CloseMapUI();
            } else {
                ShowMapUI();
            }
            isShowMaping = !isShowMaping;
        }

        if (isShowMaping) {
            UpdateMapUI();
        }
    }

    // 生成整个地面的mesh
    public Mesh GenerateGroundMesh(float height, float width) {
        Mesh mesh = new Mesh();
        // 确定顶点在哪里
        mesh.vertices = new Vector3[] {
            new Vector3(0, 0, 0),
            new Vector3(0, 0, height),
            new Vector3(width, 0, height),
            new Vector3(width, 0, 0)
        };
        // 确定哪些点形成三角形
        mesh.triangles = new int[] {
            0, 1, 2,
            0, 2, 3
        };
        mesh.uv = new Vector2[] {
            new Vector3(0, 0),
            new Vector3(0, 1),
            new Vector3(1, 1),
            new Vector3(1, 0)
        };
        return mesh;
    }

    #region 地图块相关
    // 根据观察者的位置去刷新地图块
    private void UpdateVisibleChunk() {
        // 如果观察者没有移动或者时间没到则不需要刷新
        if (viewer.position == lastViewerPosition || canUpdateChunk == false) {
            return;
        }
        lastViewerPosition = viewer.position;
        // 更新地图UI坐标
        if (isShowMaping) {
            mapUI.UpdatePivot(viewer.position);
        }
        // 更新目前可见地图块
        DoUpdateVisibleChunk();
    }

    // 更新目前可见地图块
    private void DoUpdateVisibleChunk() {
        // 当前观察者所在地图块
        Vector2Int currChunkIndex = GetMapChunkIndexByWorldPosition(viewer.position);
        // 关闭不需要显示的地图块
        for (int i = lastVisibleChunkList.Count - 1; i >= 0; i--) {
            Vector2Int chunkIndex = lastVisibleChunkList[i].chunkIndex;
            // 查看当前chunk是否需要显示, 只需要检查x,y是否在viewDistance范围里
            if (Mathf.Abs(chunkIndex.x - currChunkIndex.x) > mapConfig.viewDistance || 
                Mathf.Abs(chunkIndex.y - currChunkIndex.y) > mapConfig.viewDistance) {
                lastVisibleChunkList[i].SetActive(false);
                lastVisibleChunkList.RemoveAt(i);
            }
        }
        // 开启需要显示的地图块
        int startX = currChunkIndex.x - mapConfig.viewDistance;
        int startY = currChunkIndex.y - mapConfig.viewDistance;
        for (int x = 0; x < 2 * mapConfig.viewDistance + 1; x++) {
            for (int y = 0; y < 2 * mapConfig.viewDistance + 1; y++) {
                Vector2Int chunkIndex = new Vector2Int(startX + x, startY + y);
                // 之前加载过则直接使用dict中cache的结果, 否则需要重新绘制, 需要注意由于
                // 贴图是在协程中进行生成, 所以执行完毕后才算初始化完毕
                if (mapChunkDict.TryGetValue(chunkIndex, out MapChunkController chunk)) {
                    // 是否在显示列表中, 需要检查是否完成初始化
                    if (!lastVisibleChunkList.Contains(chunk) && chunk.isInitialized) {
                        chunk.SetActive(true);
                        lastVisibleChunkList.Add(chunk);
                    }
                } else {
                    chunk = GenerateMapChunk(chunkIndex);
                }
            }
        }
        // 控制地图块刷新时间, 当时间间隔>=UpdateVisibleChunkTime时才可进行刷新
        canUpdateChunk = false;
        // 在过了指定时间后才执行ResetCanUpdateChunkFlag重置地图块刷新标记
        Invoke(nameof(ResetCanUpdateChunkFlag), UpdateVisibleChunkTime);
    }
    
    // 通过世界坐标去获取地图块索引
    private Vector2Int GetMapChunkIndexByWorldPosition(Vector3 worldIndex) {
        int x = Mathf.Clamp(Mathf.FloorToInt(worldIndex.x / chunkSizeOnWorld), 1, mapInitData.mapSize);
        int z = Mathf.Clamp(Mathf.FloorToInt(worldIndex.z / chunkSizeOnWorld), 1, mapInitData.mapSize);
        return new Vector2Int(x, z);
    }

    // 通过世界坐标去获取地图块管理器
    public MapChunkController GetMapChunkByWorldPosition(Vector3 worldIndex) {
        return mapChunkDict[GetMapChunkIndexByWorldPosition(worldIndex)];
    }

    // 生成地图块控制器 
    private MapChunkController GenerateMapChunk(Vector2Int index, MapChunkData mapChunkData = null) {
        if (index.x > mapInitData.mapSize - 1 || index.y > mapInitData.mapSize - 1) return null;
        if (index.x < 0 || index.y < 0) return null;
        // 利用回调+lambda表达式实现协程结束后将协程中生成的地图块索引加入到UI地图块显示索引列表中
        MapChunkController chunk = mapGenerator.GenerateMapChunk(index, transform, mapChunkData, () => mapUIUpdateChunkIndexList.Add(index));
        mapChunkDict.Add(index, chunk);
        return chunk;
    }

    // 重置更新chunk的标志
    private void ResetCanUpdateChunkFlag() {
        canUpdateChunk = true;
    }

    // 在地图块刷新时生成地图对象列表, 为了让MapChunkController能访问到, 因此在这里生成了同名/同功能函数
    public List<MapObjectData> GenerateMapObjectDataOnMapChunkRefresh(Vector2Int chunkIndex) {
        return mapGenerator.GenerateMapObjectDataOnMapChunkRefresh(chunkIndex);
    }

    // 在地图块刷新时生成AI对象列表, 为了让MapChunkController能访问到, 因此在这里生成了同名/同功能函数
    public List<MapObjectData> GenerateAIObjectDataOnMapChunkRefresh(MapChunkData mapChunkData) {
        return mapGenerator.GenerateAIObjectData(mapChunkData);
    }
    #endregion

    #region 地图UI相关
    // 显示地图UI
    private void ShowMapUI() {
        mapUI = UIManager.Instance.Show<UI_MapWindow>();
        // 初始化地图UI
        if (!mapUIInitialized) {
            mapUI.InitMap(mapInitData.mapSize, mapConfig.mapChunkSize, mapSizeOnWorld, mapConfig.forestTexture);
            mapUIInitialized = true;
        }
        UpdateMapUI();
    }
    
    // 更新地图UI
    private void UpdateMapUI() {
        for (int i = 0; i < mapUIUpdateChunkIndexList.Count; i++) {
            Vector2Int chunkIndex = mapUIUpdateChunkIndexList[i];
            Texture2D texture = null;
            MapChunkController mapChunk = mapChunkDict[chunkIndex];
            if (mapChunk.isAllForest == false) {
                texture = (Texture2D)mapChunk.GetComponent<MeshRenderer>().material.mainTexture;
            }
            mapUI.AddMapChunk(chunkIndex, mapChunk.mapChunkData.mapObjectDataDict, texture);
        }
        // 当更新到UI中后就不需要当前存储的index了
        mapUIUpdateChunkIndexList.Clear();
        // 更新玩家UI坐标
        mapUI.UpdatePivot(viewer.position);
    }

    // 关闭地图UI
    private void CloseMapUI() {
        UIManager.Instance.Close<UI_MapWindow>();
    }
    #endregion

    #region 地图对象相关
    // 移除一个地图物品
    public void RemoveMapObject(ulong mapObjectId) {
        // 移除地图对象icon image
        if (mapUI != null) {
            mapUI.RemoveMapObjectIcon(mapObjectId);
        }
    }

    // 生成一个地图对象
    public void GenerateMapObject(int mapObjectConfigId, Vector3 position, bool isFromBuild) {
        Vector2Int currChunkIndex = GetMapChunkIndexByWorldPosition(position);
        GenerateMapObject(mapChunkDict[currChunkIndex], mapObjectConfigId, position, isFromBuild);
    }

    // 生成一个地图对象
    public void GenerateMapObject(
        MapChunkController mapChunkController, 
        int mapObjectConfigId, 
        Vector3 position,
        bool isFromBuild
    ) {
        // 生成数据
        MapObjectData mapObjectData = mapGenerator.GenerateMapObjectData(mapObjectConfigId, position);
        if (mapObjectData == null) {
            return;
        }
        // 将数据绑定到地图块
        mapChunkController.AddMapObject(mapObjectData, isFromBuild);
        // 添加UI icon
        if (mapUI == null) {
            return;
        }
        mapUI.AddMapObjectIcon(mapObjectData);
    }
    #endregion

    #region 地图导航相关
    [SerializeField] NavMeshSurface navMeshSurface;

    // 烘焙导航网格
    public void BakeNavMesh() {
        navMeshSurface.BuildNavMesh();
    }

    #endregion
    private void OnGameSave() {
        ArchiveManager.Instance.SaveMapData();
    }
    
    // 游戏关闭时释放场景中的资源
    public void OnCloseGameScene() {
        // 地图UI
        mapUI.ResetWindow();
        // 地图块
        foreach (MapChunkController item in mapChunkDict.Values) {
            item.OnCloseGameScene();
        }
    }
}