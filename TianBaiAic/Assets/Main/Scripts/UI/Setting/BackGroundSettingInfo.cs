using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MainSettingsUi.ConfigSettings;

namespace MainSettingsUi
{
    public class BackGroundSettingInfo : MonoBehaviour
    {
        public enum BackgroundType
        {
            DroupDown,
            InputFeld,
            Label,
            Slider
        }
        public BackgroundType backgroundType;
        public Type type;
        public ConfigSetting configSetting;
        

        // public T GetValue<T>(T defaultValue = default) where T : struct
        // {
        //     if (BackgroundType.DroupDown == backgroundType)
        //     {
        //         return (T)(object)GetComponentInChildren<TMP_Dropdown>().value;
        //     }
        //     else if (BackgroundType.InputFeld == backgroundType)
        //     {
        //         return (T)(object)GetComponentInChildren<TMP_InputField>().text;
        //     }
        //     else if (BackgroundType.Label == backgroundType)
        //     {
        //         return (T)(object)GetComponentInChildren<TMP_Text>().text;
        //     }
        //     else if (BackgroundType.Slider == backgroundType)
        //     {
        //         return (T)(object)GetComponentInChildren<Slider>().value;
        //     }
        //     return defaultValue;
        // }

    }

}
