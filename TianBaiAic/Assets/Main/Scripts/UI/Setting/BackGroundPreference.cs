using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MainSettingsUi
{
    public class BackGroundPreference : MonoBehaviour
    {
        public GameObject SettingIndix_Slider;
        public GameObject SettingIndix_InputField;
        public GameObject SettingIndix_Label;
        public GameObject SettingIndix_Dropdown;
        ConfigData configData;
        void Start()
        {
            int high = 0;
            ConfigSettings configSettings = new ConfigSettings();
            try
            {
                //检查配置设置文件是否存在（"D:\Python\Link_bai-alpha\config_init.json"）
                if (!System.IO.File.Exists(MainPreference.BackGroundPreferenceFilePathInit))
                {
                    Debug.LogError($"配置设置文件不存在: {MainPreference.BackGroundPreferenceFilePathInit}");
                    //新建配置设置文件
                    configSettings = new ConfigSettings();
                    //新建文件
                    System.IO.File.WriteAllText(MainPreference.BackGroundPreferenceFilePathInit, JsonConvert.SerializeObject(configSettings, Formatting.Indented));
                    Debug.Log($"新建配置设置文件: {MainPreference.BackGroundPreferenceFilePathInit}");
                }
                else
                {
                    //如果配置设置文件存在，则读取配置设置文件
                    string configSettingsJson = System.IO.File.ReadAllText(MainPreference.BackGroundPreferenceFilePathInit);
                    configSettings = JsonConvert.DeserializeObject<ConfigSettings>(configSettingsJson);
                    if (configSettings == null)
                    {
                        Debug.LogError($"配置设置文件解析失败: {MainPreference.BackGroundPreferenceFilePathInit}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"读取配置设置文件失败: {MainPreference.BackGroundPreferenceFilePathInit}, 错误: {ex.Message}");
            }

            //检查配置文件是否存在
            if (!System.IO.File.Exists(MainPreference.BackGroundPreferenceFilePath))
            {
                Debug.LogError($"配置文件不存在: {MainPreference.BackGroundPreferenceFilePath}");
                return;
            }

            //解析config键值对json文件
            string json = System.IO.File.ReadAllText(MainPreference.BackGroundPreferenceFilePath);
            configData = new ConfigData(json);

            //遍历所有键值对
            foreach (var key in configData.GetKeys())
            {
                //获取键值对的类型
                JsonDataType type = configData.GetType(key);
                GameObject createdObject = null;
                //读取配置数据的值
                if (!configSettings.GetConfigSetting(key).Show)
                {
                    continue; //如果配置设置中不显示，则跳过
                }
                string showName = configSettings.GetConfigSetting(key).ShowName;
                if (type == JsonDataType.String || configSettings.GetConfigSetting(key).ShowAsString)
                {
                    //如果是字符串类型且配置设置中显示为字符串，则创建输入框
                    createdObject = Instantiate(SettingIndix_InputField, transform);
                    createdObject.name = key;
                    createdObject.GetComponentInChildren<TMP_InputField>().text = configData.GetValue<string>(key, "");
                    createdObject.GetComponentInChildren<TextMeshProUGUI>().text = showName;
                    if (configSettings.GetConfigSetting(key).ShowAsString && type != JsonDataType.String)
                    {
                        // 如果配置设置中显示为字符串且类型不是字符串，则进行限制输入处理
                        if (type == JsonDataType.Integer)
                        {
                            createdObject.GetComponentInChildren<TMP_InputField>().contentType = TMP_InputField.ContentType.IntegerNumber; // 限制为整数输入
                        }
                        else if (type == JsonDataType.Float)
                        {
                            createdObject.GetComponentInChildren<TMP_InputField>().contentType = TMP_InputField.ContentType.DecimalNumber; // 限制为浮点数输入
                        }
                        else
                        {
                            createdObject.GetComponentInChildren<TMP_InputField>().contentType = TMP_InputField.ContentType.Standard; // 默认文本输入
                        }
                    }
                    createdObject.GetComponent<BackGroundSettingInfo>().backgroundType = BackGroundSettingInfo.BackgroundType.InputFeld;
                }
                //根据类型创建对应的UI元素
                else if (type == JsonDataType.Integer || type == JsonDataType.Float)
                {
                    createdObject = Instantiate(SettingIndix_Slider, transform);
                    createdObject.name = key;
                    createdObject.GetComponentInChildren<Slider>().value = configData.GetValue<float>(key, 0f);
                    if (type == JsonDataType.Integer)
                    {
                        createdObject.GetComponentInChildren<Slider>().wholeNumbers = true; // 如果是整数类型，设置滑动条为整数
                    }
                    createdObject.transform.Find("SliderWithText").transform.Find("Text").GetComponent<TextMeshProUGUI>().text = configData.GetValue<float>(key, 0f).ToString();
                    createdObject.GetComponentInChildren<TextMeshProUGUI>().text = showName;
                    createdObject.GetComponent<BackGroundSettingInfo>().backgroundType = BackGroundSettingInfo.BackgroundType.Slider;
                }
                else if (type == JsonDataType.Boolean)
                {
                    createdObject = Instantiate(SettingIndix_Label, transform);
                    createdObject.name = key;
                    createdObject.GetComponentInChildren<Toggle>().isOn = configData.GetValue<bool>(key, false);
                    createdObject.GetComponentInChildren<TextMeshProUGUI>().text = showName;
                    createdObject.GetComponent<BackGroundSettingInfo>().backgroundType = BackGroundSettingInfo.BackgroundType.Label;
                }
                else if (type == JsonDataType.Array)
                {
                    createdObject = Instantiate(SettingIndix_Dropdown, transform);
                    createdObject.name = key;
                    var options = configData.GetValue<List<string>>(key, new List<string>());
                    createdObject.GetComponentInChildren<TMP_Dropdown>().AddOptions(options);
                    createdObject.GetComponentInChildren<TextMeshProUGUI>().text = showName;
                    createdObject.GetComponent<BackGroundSettingInfo>().backgroundType = BackGroundSettingInfo.BackgroundType.DroupDown;
                }
                if (createdObject != null)
                {
                    //设置UI元素的父物体为当前物体
                    createdObject.transform.SetParent(transform, false);
                    high += 50;
                    //设置UI元素的键名
                    createdObject.name = key;
                    createdObject.GetComponent<BackGroundSettingInfo>().type = type.GetType();
                    createdObject.GetComponent<BackGroundSettingInfo>().configSetting = configSettings.GetConfigSetting(key);
                }

            }
            //序列化config_info.json文件
            string configInfoJson = JsonConvert.SerializeObject(configSettings, Formatting.Indented);
            //写入config_info.json文件
            System.IO.File.WriteAllText(MainPreference.BackGroundPreferenceFilePathInit, configInfoJson);
            Debug.Log($"配置设置已保存到: {MainPreference.BackGroundPreferenceFilePathInit}");
            //设置当前物体的高度
            RectTransform rectTransform = GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, high);
        }
        public void Apply()
        {
            //遍历自己的所有子物体
            foreach (Transform child in transform)
            {
                BackGroundSettingInfo settingInfo = child.GetComponent<BackGroundSettingInfo>();
                if (settingInfo != null)
                {
                    //根据类型获取值
                    if (settingInfo.backgroundType == BackGroundSettingInfo.BackgroundType.InputFeld)
                    {
                        string value = child.GetComponentInChildren<TMP_InputField>().text;
                        configData.SetValue(settingInfo.name, value);
                    }
                    else if (settingInfo.backgroundType == BackGroundSettingInfo.BackgroundType.Slider)
                    {
                        if (child.GetComponentInChildren<Slider>().wholeNumbers)
                        {
                            int value = (int)child.GetComponentInChildren<Slider>().value;
                            configData.SetValue(settingInfo.name, value);
                        }
                        else
                        {
                            float value = child.GetComponentInChildren<Slider>().value;
                            configData.SetValue(settingInfo.name, value);
                        }
                    }
                    else if (settingInfo.backgroundType == BackGroundSettingInfo.BackgroundType.Label)
                    {
                        bool value = child.GetComponentInChildren<Toggle>().isOn;
                        configData.SetValue(settingInfo.name, value);
                    }
                    else if (settingInfo.backgroundType == BackGroundSettingInfo.BackgroundType.DroupDown)
                    {
                        int value = child.GetComponentInChildren<TMP_Dropdown>().value;
                        configData.SetValue(settingInfo.name, value);
                    }
                }
            }
            //序列化config.json文件
            string configJson = configData.ToJson(true);
            //写入config.json文件
            System.IO.File.WriteAllText(MainPreference.BackGroundPreferenceFilePath, configJson);
        }

        public void SetProJect()
        {

            MainPreference.BackGroundProjectPath = configData.GetValue<string>("Project_Path", MainPreference.BackGroundProjectPath);
        }
    }

    public enum JsonDataType
    {
        Undefined,
        Object,
        Array,
        String,
        Integer,
        Float,
        Boolean,
        Null
    }

    public class ConfigData
    {
        private JObject _jsonObject; // 私有字段，存储实际的 JSON 对象

        // 构造函数：初始化为空的 JObject
        public ConfigData()
        {
            _jsonObject = new JObject();
        }

        // 构造函数：从 JSON 字符串初始化
        public ConfigData(string jsonString)
        {
            FromJson(jsonString);
        }

        /// <summary>
        /// 从 JSON 字符串解析数据，并更新内部的 JObject。
        /// </summary>
        /// <param name="jsonString">待解析的 JSON 字符串。</param>
        public void FromJson(string jsonString)
        {
            if (string.IsNullOrWhiteSpace(jsonString))
            {
                _jsonObject = new JObject(); // 如果字符串为空，则初始化为空对象
                return;
            }

            try
            {
                _jsonObject = JObject.Parse(jsonString);
            }
            catch (JsonException ex)
            {
                // 处理 JSON 解析错误，例如打印日志或抛出自定义异常
                UnityEngine.Debug.LogError($"Error parsing JSON string: {ex.Message}");
                _jsonObject = new JObject(); // 解析失败时，也初始化为空对象，避免空引用
            }
        }

        /// <summary>
        /// 将内部的 JObject 转换为 JSON 字符串。
        /// </summary>
        /// <param name="indented">是否格式化输出 (带缩进)。</param>
        /// <returns>JSON 字符串。</returns>
        public string ToJson(bool indented = true)
        {
            if (_jsonObject == null)
            {
                return "{}"; // 如果没有数据，返回空 JSON 对象字符串
            }
            return _jsonObject.ToString(indented ? Formatting.Indented : Formatting.None);
        }

        /// <summary>
        /// 获取指定键的 JSON 值的类型。
        /// </summary>
        /// <param name="key">键名。</param>
        /// <returns>JsonDataType 枚举表示的类型。</returns>
        public JsonDataType GetType(string key)
        {
            if (_jsonObject == null || !_jsonObject.ContainsKey(key))
            {
                return JsonDataType.Undefined;
            }

            JToken token = _jsonObject[key];
            if (token == null) return JsonDataType.Null; // 键存在但值为 null

            switch (token.Type)
            {
                case JTokenType.Object:
                    return JsonDataType.Object;
                case JTokenType.Array:
                    return JsonDataType.Array;
                case JTokenType.String:
                    return JsonDataType.String;
                case JTokenType.Integer:
                    return JsonDataType.Integer;
                case JTokenType.Float:
                    return JsonDataType.Float;
                case JTokenType.Boolean:
                    return JsonDataType.Boolean;
                case JTokenType.Null:
                    return JsonDataType.Null;
                default:
                    return JsonDataType.Undefined;
            }
        }

        /// <summary>
        /// 获取所有键的列表。
        /// </summary>
        /// <returns>包含所有键名的 List<string>。</returns>
        public List<string> GetKeys()
        {
            if (_jsonObject == null)
            {
                return new List<string>();
            }
            return new List<string>(_jsonObject.Properties().Select(p => p.Name));
        }

        /// <summary>
        /// 获取指定键的索引。注意：JObject 是无序的，此索引是根据当前 JObject 的内部迭代顺序生成。
        /// 不建议依赖此索引进行频繁访问，因为顺序可能因操作而改变。
        /// </summary>
        /// <param name="key">要查找的键名。</param>
        /// <returns>键的索引，如果未找到则返回 -1。</returns>
        public int GetIndex(string key)
        {
            if (_jsonObject == null) return -1;
            int index = 0;
            foreach (var property in _jsonObject.Properties())
            {
                if (property.Name == key)
                {
                    return index;
                }
                index++;
            }
            return -1;
        }

        /// <summary>
        /// 通过键名获取值。
        /// </summary>
        /// <param name="key">键名。</param>
        /// <returns>值的 object 闭包，如果键不存在则返回 null。</returns>
        public object GetValue(string key)
        {
            if (_jsonObject == null || !_jsonObject.ContainsKey(key))
            {
                return null;
            }
            JToken token = _jsonObject[key];
            return token?.ToObject<object>(); // 将 JToken 转换为 object
        }

        /// <summary>
        /// 通过索引获取值。注意：JObject 是无序的，此索引仅在当前迭代时有效。
        /// </summary>
        /// <param name="index">索引。</param>
        /// <returns>值的 object 闭包，如果索引超出范围则返回 null。</returns>
        public object GetValue(int index)
        {
            if (_jsonObject == null || index < 0 || index >= _jsonObject.Count)
            {
                return null;
            }

            // JObject 自身不支持通过索引直接访问，需要迭代
            int currentIndex = 0;
            foreach (var property in _jsonObject.Properties())
            {
                if (currentIndex == index)
                {
                    return property.Value?.ToObject<object>();
                }
                currentIndex++;
            }
            return null; // 理论上不会走到这里，除非索引和 Count 不匹配
        }

        /// <summary>
        /// 设置指定键的值。如果键不存在则添加，如果存在则更新。
        /// </summary>
        /// <param name="key">键名。</param>
        /// <param name="value">要设置的值。</param>
        public void SetValue(string key, object value)
        {
            if (_jsonObject == null)
            {
                _jsonObject = new JObject(); // 确保 JObject 已初始化
            }

            // JToken.FromObject 可以将任何 C# 对象转换为 JToken
            _jsonObject[key] = value != null ? JToken.FromObject(value) : null;
        }

        /// <summary>
        /// 通过索引设置值。注意：JObject 是无序的，不推荐通过索引进行修改。
        /// 此方法会尝试找到对应索引的键并更新其值。
        /// </summary>
        /// <param name="index">索引。</param>
        /// <param name="value">要设置的值。</param>
        public void SetValue(int index, object value)
        {
            if (_jsonObject == null || index < 0 || index >= _jsonObject.Count)
            {
                UnityEngine.Debug.LogWarning($"Attempted to set value at invalid index: {index}. JObject count: {_jsonObject?.Count ?? 0}");
                return;
            }

            int currentIndex = 0;
            foreach (var property in _jsonObject.Properties())
            {
                if (currentIndex == index)
                {
                    property.Value = value != null ? JToken.FromObject(value) : null;
                    return;
                }
                currentIndex++;
            }
        }

        /// <summary>
        /// 尝试获取特定类型的值。
        /// </summary>
        /// <typeparam name="T">期望的值类型。</typeparam>
        /// <param name="key">键名。</param>
        /// <param name="defaultValue">如果获取失败或转换失败时返回的默认值。</param>
        /// <returns>转换后的值，或默认值。</returns>
        public T GetValue<T>(string key, T defaultValue = default(T))
        {
            if (_jsonObject == null || !_jsonObject.ContainsKey(key))
            {
                return defaultValue;
            }
            JToken token = _jsonObject[key];
            try
            {
                return token.Value<T>(); // Newtonsoft.Json 的强大之处
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"Could not convert value for key '{key}' to type {typeof(T).Name}. Error: {ex.Message}");
                return defaultValue;
            }
        }

        /// <summary>
        /// 尝试通过索引获取特定类型的值。
        /// </summary>
        /// <typeparam name="T">期望的值类型。</typeparam>
        /// <param name="index">索引。</param>
        /// <param name="defaultValue">如果获取失败或转换失败时返回的默认值。</param>
        /// <returns>转换后的值，或默认值。</returns>
        public T GetValue<T>(int index, T defaultValue = default(T))
        {
            if (_jsonObject == null || index < 0 || index >= _jsonObject.Count)
            {
                return defaultValue;
            }

            int currentIndex = 0;
            foreach (var property in _jsonObject.Properties())
            {
                if (currentIndex == index)
                {
                    try
                    {
                        return property.Value.Value<T>();
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogWarning($"Could not convert value at index '{index}' (key: {property.Name}) to type {typeof(T).Name}. Error: {ex.Message}");
                        return defaultValue;
                    }
                }
                currentIndex++;
            }
            return defaultValue;
        }

        /// <summary>
        /// 检查是否存在指定的键。
        /// </summary>
        /// <param name="key">键名。</param>
        /// <returns>如果存在则为 true，否则为 false。</returns>
        public bool ContainsKey(string key)
        {
            return _jsonObject != null && _jsonObject.ContainsKey(key);
        }

        /// <summary>
        /// 移除指定键及其值。
        /// </summary>
        /// <param name="key">要移除的键。</param>
        /// <returns>如果成功移除则为 true，否则为 false。</returns>
        public bool Remove(string key)
        {
            if (_jsonObject == null) return false;
            return _jsonObject.Remove(key);
        }

        /// <summary>
        /// 获取 JSON 对象中键值对的数量。
        /// </summary>
        public int Count
        {
            get { return _jsonObject?.Count ?? 0; }
        }
    }

    [Serializable]
    public class ConfigSettings
    {
        public List<ConfigSetting> Settings = new List<ConfigSetting>();
        //维护迭代器
        public IEnumerator<ConfigSetting> GetEnumerator() => Settings.GetEnumerator();
        public ConfigSetting GetConfigSetting(string name)
        {
            foreach (var setting in Settings)
            {
                if (setting.Name == name)
                {
                    return setting;
                }
            }
            //新建一个配置设置
            ConfigSetting newSetting = new ConfigSetting { Name = name, ShowName = name };
            Settings.Add(newSetting);
            return newSetting;
        }
        public void AddConfigSetting(ConfigSetting setting)
        {
            if (GetConfigSetting(setting.Name) == null)
            {
                Settings.Add(setting);
            }
            else
            {
                Debug.LogWarning($"ConfigSetting with name '{setting.Name}' already exists.");
            }
        }
        [Serializable]
        public class ConfigSetting
        {
            public bool Show = true;
            public string Name = "";
            public string ShowName = "";
            public bool ShowAsString = false; // 是否以字符串形式显示
        }

    }


}
public static class MainPreference
{
    public static string BackGroundPreferenceFilePath = @"D:\Python\Link_bai-alpha\config.json";
    public static string BackGroundPreferenceFilePathInit = @"D:\Python\Link_bai-alpha\config_init.json";
    public static string BackGroundProjectPath = @"D:\Python\Link_bai-alpha";
}