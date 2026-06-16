using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "ColorPallete", menuName = "ScriptableObjects/Color/ColorPallete")]
public class ColorPallete : ScriptableObject
{
    public Dictionary<CubeShooterColor, Material> colorDictionary = new Dictionary<CubeShooterColor, Material>();

    public List<CubeShooterColor> colorKeys = new List<CubeShooterColor>();
    public List<Material> colorValues = new List<Material>();

    public void OnEnable()
    {
        colorDictionary.Clear();
        for (int i = 0; i < Mathf.Min(colorKeys.Count, colorValues.Count); i++)
        {
            colorDictionary[colorKeys[i]] = colorValues[i];
        }
    }


    [Header("Special Materials")]
    public Material HiddenMaterial;
    public Color HiddenLineColor;

    public Color GetColorBase(CubeShooterColor color)
    {
        if (!colorDictionary.TryGetValue(color, out Material material) || material == null)
        {
            Debug.LogError($"Color {color} not found in ColorPallete!");
            return Color.white;
        }
        
        if (material.HasProperty("_Color"))
        {
            return material.GetColor("_Color");
        }
        
        if (material.HasProperty("_BaseColor"))
        {
            return material.GetColor("_BaseColor");
        }
        
        if (material.HasProperty("_HColor")) // Toony Colors Pro 2 highlight/base color
        {
            return material.GetColor("_HColor");
        }
        
        Debug.LogWarning($"Material '{material.name}' with Shader '{material.shader.name}' doesn't have a recognized color property. Returning Color.white as fallback.");
        return Color.white;
    }
    
#if UNITY_EDITOR
    [System.Serializable]
    public class NewColorEntry
    {
        
        public string name;
        public Material color;
    }

    
    
    
    public List<NewColorEntry> newColors = new List<NewColorEntry>();

    
    
    
    private void AddColorsToEnum()
    {
        if (newColors == null || newColors.Count == 0)
        {
            Debug.LogError("List is empty. Vui lòng thêm ít nhất 1 màu.");
            return;
        }

        string enumFilePath = "Assets/_FrameWork/Scripts/Data/ColorData/CubeShooterColor.cs";
        string fullPath = System.IO.Path.GetFullPath(enumFilePath);
        
        if (!System.IO.File.Exists(fullPath))
        {
            Debug.LogError("Could not find CubeShooterColor.cs at " + fullPath);
            return;
        }

        string[] lines = System.IO.File.ReadAllLines(fullPath);
        int enumStartLine = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("enum CubeShooterColor"))
            {
                enumStartLine = i;
                break;
            }
        }

        if (enumStartLine == -1)
        {
            Debug.LogError("Could not find enum CubeShooterColor in CubeShooterColor.cs");
            return;
        }

        int insertIndex = -1;
        for (int i = enumStartLine; i < lines.Length; i++)
        {
            if (lines[i].Contains("}"))
            {
                insertIndex = i;
                break;
            }
        }

        if (insertIndex != -1)
        {
            var validEntries = new List<NewColorEntry>();
            var newNamesToAdd = new List<string>();
            bool directDictionaryAdded = false;

            foreach (var entry in newColors)
            {
                if (entry.color == null)
                {
                    Debug.LogWarning("Bỏ qua phần tử có Material bị null.");
                    continue;
                }

                string nameToUse = entry.name;
                if (string.IsNullOrEmpty(nameToUse))
                {
                    nameToUse = entry.color.name;
                }

                string cleanName = GetCleanIdentifier(nameToUse);
                if (string.IsNullOrEmpty(cleanName))
                {
                    Debug.LogWarning($"Bỏ qua phần tử do không tạo được định danh hợp lệ cho tên: '{nameToUse}'");
                    continue;
                }

                // Cập nhật lại name đã làm sạch cho hiển thị
                entry.name = cleanName;

                // Kiểm tra xem enum đã định nghĩa màu này chưa
                bool isDefined = System.Enum.IsDefined(typeof(CubeShooterColor), cleanName);

                if (isDefined)
                {
                    // Nếu màu đã có sẵn trong enum, thêm hoặc cập nhật trực tiếp vào dictionary ngay lập tức
                    CubeShooterColor existingEnumVal = (CubeShooterColor)System.Enum.Parse(typeof(CubeShooterColor), cleanName);
                    if (colorDictionary.ContainsKey(existingEnumVal))
                    {
                        colorDictionary[existingEnumVal] = entry.color;
                        Debug.Log($"<color=cyan>Đã cập nhật Material cho màu sẵn có '{cleanName}' trong ColorPallete.</color>");
                    }
                    else
                    {
                        colorDictionary.Add(existingEnumVal, entry.color);
                        Debug.Log($"<color=green>Đã thêm màu sẵn có '{cleanName}' vào ColorPallete dictionary.</color>");
                    }
                    directDictionaryAdded = true;
                    continue;
                }

                // Kiểm tra trùng lặp trong danh sách chuẩn bị thêm
                if (newNamesToAdd.Contains(cleanName))
                {
                    Debug.LogWarning($"Màu '{cleanName}' bị trùng lặp trong danh sách thêm mới!");
                    continue;
                }

                validEntries.Add(entry);
                newNamesToAdd.Add(cleanName);
            }

            // Nếu có cập nhật trực tiếp vào dictionary, lưu lại luôn
            if (directDictionaryAdded)
            {
                EditorUtility.SetDirty(this);
                AssetDatabase.SaveAssets();
            }

            if (validEntries.Count == 0)
            {
                // Nếu tất cả các màu đều được thêm trực tiếp hoặc không hợp lệ, ta dọn dẹp list và dừng lại
                newColors.Clear();
                return;
            }

            // Tìm giá trị số lớn nhất hiện tại của enum để cộng dồn
            int maxVal = 0;
            foreach (var val in System.Enum.GetValues(typeof(CubeShooterColor)))
            {
                if ((int)val > maxVal)
                {
                    maxVal = (int)val;
                }
            }

            // Đảm bảo phần tử trước vị trí chèn kết thúc bằng dấu phẩy
            for (int i = insertIndex - 1; i >= 0; i--)
            {
                if (!string.IsNullOrWhiteSpace(lines[i]))
                {
                    if (!lines[i].TrimEnd().EndsWith(","))
                    {
                        lines[i] = lines[i] + ",";
                    }
                    break;
                }
            }

            var list = new System.Collections.Generic.List<string>(lines);
            
            for (int i = 0; i < newNamesToAdd.Count; i++)
            {
                int newValue = maxVal + 1 + i;
                string lineToAdd = $"    {newNamesToAdd[i]} = {newValue},";
                list.Insert(insertIndex + i, lineToAdd);
            }

            System.IO.File.WriteAllLines(fullPath, list.ToArray());

            // Serialize dữ liệu để nạp lại sau khi compile xong
            string serializedData = "";
            foreach (var entry in validEntries)
            {
                string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(entry.color));
                serializedData += $"{entry.name}:{guid};";
            }

            EditorPrefs.SetString("AddColors_Data", serializedData);
            EditorPrefs.SetString("AddColors_Path", AssetDatabase.GetAssetPath(this));

            Debug.Log($"Đã ghi thêm {validEntries.Count} màu vào enum CubeShooterColor. Đang đợi Unity biên dịch...");
            AssetDatabase.Refresh();
        }
    }

    private string GetCleanIdentifier(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        
        // Loại bỏ các tiền tố Material phổ biến
        if (input.StartsWith("Mat_", System.StringComparison.OrdinalIgnoreCase))
            input = input.Substring(4);
        else if (input.StartsWith("M_", System.StringComparison.OrdinalIgnoreCase))
            input = input.Substring(2);

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                if (sb.Length == 0 && char.IsDigit(c))
                {
                    sb.Append('_');
                }
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    [UnityEditor.Callbacks.DidReloadScripts]
    private static void OnScriptsReloaded()
    {
        if (EditorPrefs.HasKey("AddColors_Data"))
        {
            string data = EditorPrefs.GetString("AddColors_Data");
            string assetPath = EditorPrefs.GetString("AddColors_Path");

            EditorPrefs.DeleteKey("AddColors_Data");
            EditorPrefs.DeleteKey("AddColors_Path");

            ColorPallete pallete = AssetDatabase.LoadAssetAtPath<ColorPallete>(assetPath);
            if (pallete != null && !string.IsNullOrEmpty(data))
            {
                string[] entries = data.Split(new char[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries);
                bool addedAny = false;

                foreach (string entry in entries)
                {
                    string[] parts = entry.Split(':');
                    if (parts.Length == 2)
                    {
                        string colorName = parts[0];
                        string colorHtml = parts[1];

                        try 
                        {
                            CubeShooterColor newEnumVal = (CubeShooterColor)System.Enum.Parse(typeof(CubeShooterColor), colorName);
                            string path = AssetDatabase.GUIDToAssetPath(colorHtml); // colorHtml is actually GUID here
                            Material colorVal = AssetDatabase.LoadAssetAtPath<Material>(path);
                            
                            if (colorVal != null)
                            {
                                if (!pallete.colorDictionary.ContainsKey(newEnumVal))
                                {
                                    pallete.colorDictionary.Add(newEnumVal, colorVal);
                                    addedAny = true;
                                    Debug.Log($"<color=green>Successfully added '{colorName}' to ColorPallete dictionary!</color>");
                                }
                            }
                            else
                            {
                                Debug.LogWarning($"Could not find Material at path: {path} for {colorName}");
                            }
                        }
                        catch (System.Exception e)
                        {
                            Debug.LogError($"Failed to parse enum '{colorName}' after compile: " + e.Message);
                        }
                    }
                }

                if (addedAny)
                {
                    pallete.newColors.Clear(); // Xoá danh sách sau khi đã add thành công
                    EditorUtility.SetDirty(pallete);
                    AssetDatabase.SaveAssets();
                }
            }
        }
    }
#endif
}
