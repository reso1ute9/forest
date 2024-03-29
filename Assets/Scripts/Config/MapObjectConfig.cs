using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using JKFrame;
using Sirenix.OdinInspector;


[CreateAssetMenu(fileName = "地图物体配置", menuName = "Config/地图物体配置")]
public class MapObjectConfig : ConfigBase
{
    [LabelText("空的(不生成物品)")]
    public bool isEmpty = false;
    [LabelText("地图顶点类型")]
    public MapVertexType mapVertexType;
    [LabelText("预制体")]
    public GameObject prefab;
    [LabelText("物体icon")]             
    public Sprite mapIconSprite;
    [LabelText("物体icon尺寸")]
    public float mapIconSize = 1.0f;            // 0代表不生成物品
    [LabelText("描述"), MultiLineProperty] 
    public string descript;
    [LabelText("生成概率(百分比类型)")]
    public int probability;
    [LabelText("腐烂天数")]
    public int destoryDay = -1;                 // -1代表无效
}
