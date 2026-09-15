using BepInEx;
using BepInEx.Unity.Mono;
using HarmonyLib;
using I2.Loc;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro; // Подключаем TextMeshPro

namespace StackmonRuFix
{
    [BepInPlugin("com.zvx.stackmon.ru", "Stackmon RU Translation", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static Dictionary<string, string> CustomTranslations = new Dictionary<string, string>();
        public static Sprite? CustomTrainerSprite;
        public static Font? CustomFont;
        public static TMP_FontAsset? CustomTmpFont; // Кеш для шрифта TextMeshPro

        private void Awake()
        {
            // Жёстко задаем путь: BepInEx/plugins/ZVX-RUS
            string pluginDir = Path.Combine(Paths.PluginPath, "ZVX-RUS");

            // Если папки ZVX-RUS вдруг нет - создаем её
            if (!Directory.Exists(pluginDir))
            {
                Directory.CreateDirectory(pluginDir);
            }

            // -----------------------------------------------------------
            // 1. ЗАГРУЗКА ПЕРЕВОДА (из ZVX-RUS/ZVX_Translation.txt)
            // -----------------------------------------------------------
            string translationFilePath = Path.Combine(pluginDir, "ZVX_Translation.txt");
            if (File.Exists(translationFilePath))
            {
                string[] lines = File.ReadAllLines(translationFilePath);
                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//") || !line.Contains("="))
                        continue;

                    int separatorIndex = line.IndexOf('=');
                    string term = line.Substring(0, separatorIndex).Trim();
                    string translation = line.Substring(separatorIndex + 1).Trim().Replace("\\n", "\n");

                    CustomTranslations[term] = translation;
                }
                Logger.LogInfo($"[ZVX] Успешно загружено строк перевода: {CustomTranslations.Count}");
            }
            else
            {
                Logger.LogWarning($"[ZVX] Файл перевода не найден! Создаю шаблон: {translationFilePath}");
                File.WriteAllText(translationFilePath, "// Формат: Ключ=Перевод\nCards/Places/PLACE/GYMWATER/TITLE=Водяная Арена\n");
            }

            // -----------------------------------------------------------
            // 2. ЗАГРУЗКА ИЗОБРАЖЕНИЯ (из ZVX-RUS/img/)
            // -----------------------------------------------------------
            string imgDir = Path.Combine(pluginDir, "img");
            if (!Directory.Exists(imgDir)) Directory.CreateDirectory(imgDir);

            string imagePath = Path.Combine(imgDir, "SPR_Menu_Front_Trainer.png");
            if (File.Exists(imagePath))
            {
                byte[] fileData = File.ReadAllBytes(imagePath);
                Texture2D tex = new Texture2D(2, 2);
                ImageConversion.LoadImage(tex, fileData);

                CustomTrainerSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                CustomTrainerSprite.name = "SPR_Menu_Front_Trainer_RU";

                Logger.LogInfo("[ZVX] Изображение SPR_Menu_Front_Trainer успешно загружено!");
            }

            // -----------------------------------------------------------
            // 3. ЗАГРУЗКА ШРИФТА (из ZVX-RUS/font/)
            // -----------------------------------------------------------
            string fontDir = Path.Combine(pluginDir, "font");

            if (!Directory.Exists(fontDir))
            {
                Directory.CreateDirectory(fontDir);
            }
            else
            {
                string[] bundles = Directory.GetFiles(fontDir, "*.bundle");
                if (bundles.Length > 0)
                {
                    AssetBundle bundle = AssetBundle.LoadFromFile(bundles[0]);
                    if (bundle != null)
                    {
                        Font[] loadedFonts = bundle.LoadAllAssets<Font>();
                        if (loadedFonts.Length > 0)
                        {
                            CustomFont = loadedFonts[0];

                            // Автоматически конвертируем обычный шрифт в формат TextMeshPro прямо на лету
                            CustomTmpFont = TMP_FontAsset.CreateFontAsset(CustomFont);

                            Logger.LogInfo($"[ZVX] Шрифт {CustomFont.name} успешно загружен и подготовлен для TextMeshPro!");
                        }

                        bundle.Unload(false);
                    }
                }
            }

            Harmony.CreateAndPatchAll(typeof(Plugin));
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // -----------------------------------------------------------
        // ПАТЧ ЛОКАЛИЗАЦИИ
        // -----------------------------------------------------------
        [HarmonyPatch(typeof(LocalizationManager), nameof(LocalizationManager.UpdateSources))]
        [HarmonyPostfix]
        public static void InjectRussianTranslation()
        {
            if (LocalizationManager.Sources.Count == 0) return;

            LanguageSourceData source = LocalizationManager.Sources[0];
            int rusIndex = source.GetLanguageIndex("Russian");
            if (rusIndex == -1) rusIndex = source.GetLanguageIndex("Русский");
            if (rusIndex == -1) rusIndex = 1;

            foreach (var kvp in CustomTranslations)
            {
                TermData termData = source.GetTermData(kvp.Key);
                if (termData != null && rusIndex < termData.Languages.Length)
                {
                    termData.Languages[rusIndex] = kvp.Value;
                }
            }
        }

        // -----------------------------------------------------------
        // ПОДМЕНА КАРТИНКИ И ШРИФТА ПРИ ЗАГРУЗКЕ СЦЕНЫ
        // -----------------------------------------------------------
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Подмена картинок
            if (CustomTrainerSprite != null)
            {
                Image[] uiImages = Resources.FindObjectsOfTypeAll<Image>();
                foreach (Image img in uiImages)
                {
                    if (img.sprite != null && img.sprite.name == "SPR_Menu_Front_Trainer")
                    {
                        img.sprite = CustomTrainerSprite;
                    }
                }

                SpriteRenderer[] spriteRenderers = Resources.FindObjectsOfTypeAll<SpriteRenderer>();
                foreach (SpriteRenderer sr in spriteRenderers)
                {
                    if (sr.sprite != null && sr.sprite.name == "SPR_Menu_Front_Trainer")
                    {
                        sr.sprite = CustomTrainerSprite;
                    }
                }
            }

            // Подмена обычных шрифтов Unity UI
            if (CustomFont != null)
            {
                Text[] allTexts = Resources.FindObjectsOfTypeAll<Text>();
                foreach (Text txt in allTexts)
                {
                    if (txt != null) txt.font = CustomFont;
                }
            }

            // Подмена шрифтов TextMeshPro (главный тип текста в игре)
            if (CustomTmpFont != null)
            {
                TMP_Text[] allTmpTexts = Resources.FindObjectsOfTypeAll<TMP_Text>();
                foreach (TMP_Text tmpTxt in allTmpTexts)
                {
                    if (tmpTxt != null) tmpTxt.font = CustomTmpFont;
                }
            }
        }
    }
}