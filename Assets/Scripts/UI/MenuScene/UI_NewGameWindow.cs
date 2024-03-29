using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using JKFrame;


[UIElement(false, "UI/UI_NewGameWindow", 1)]
public class UI_NewGameWindow : UI_WindowBase
{
    [SerializeField] Slider mapSize_Slider;
    [SerializeField] InputField mapSeed_InputField; 
    [SerializeField] InputField spwanSeed_InputField; 
    [SerializeField] Slider marshLimit_Slider; 
    [SerializeField] Button backMenu_Button;
    [SerializeField] Button startGame_Button;

    public override void Init()
    {
        // 框架中的Close函数
        backMenu_Button.onClick.AddListener(Close);
        startGame_Button.onClick.AddListener(StartGame);

        backMenu_Button.BindMouseEffect();
        startGame_Button.BindMouseEffect();
    }

    public override void OnClose()
    {
        base.OnClose();
        backMenu_Button.RemoveMosueEffect();
        startGame_Button.RemoveMosueEffect();
        UIManager.Instance.Show<UI_MenuScenceMainWindow>();
    }

    private void StartGame() {
        int mapSize = (int)mapSize_Slider.value;
        // 如果玩家不输入种子的值则随机
        int mapSeed = string.IsNullOrEmpty(mapSeed_InputField.text) ? UnityEngine.Random.Range(int.MinValue, int.MaxValue) : int.Parse(mapSeed_InputField.text);
        int spwanSeed = string.IsNullOrEmpty(spwanSeed_InputField.text) ? UnityEngine.Random.Range(int.MinValue, int.MaxValue) : int.Parse(spwanSeed_InputField.text);
        float marshLimit = marshLimit_Slider.value;

        // 关闭所有窗口
        UIManager.Instance.CloseAll();
        // TODO:建立新存档并开始游戏
        GameManager.Instance.CreateNewArchive_EnterGame(mapSize, mapSeed, spwanSeed, marshLimit);
    }
}
