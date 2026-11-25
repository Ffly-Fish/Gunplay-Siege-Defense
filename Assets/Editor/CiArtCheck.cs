using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// CiArtCheck.cs —— 素材清单校验：逐项检查 Resources/Art 下是否已放置清单要求的文件
namespace Gunplay.EditorTools
{
    /// <summary>
    /// 素材清单校验工具（不依赖运行场景，菜单或批处理均可跑）。
    /// 检查 Assets/Resources/Art/{Sprites,Audio} 下是否存在 docs/素材清单.md 要求的 key，
    /// 把缺失清单写入 E:/UnityProject/_ci/artcheck.log，便于用户对照补齐。
    /// </summary>
    public static class CiArtCheck
    {
        /// <summary>校验日志路径（批处理与本地共用）</summary>
        private const string LogFilePath = "E:/UnityProject/_ci/artcheck.log";

        /// <summary>P0 必需精灵 key（决定基础风格）</summary>
        private static readonly string[] RequiredSpriteKeys =
        {
            "bg", "wall", "cannon",
            "enemy_grunt", "enemy_runner", "enemy_shield", "enemy_flyer", "enemy_heavy", "enemy_boss",
            "bullet", "grenade", "explosion", "hit_spark", "muzzle", "crosshair", "coin",
        };

        /// <summary>P1 建议精灵 key（界面与反馈质感）</summary>
        private static readonly string[] OptionalSpriteKeys =
        {
            "bullet_laser", "shield_break", "shield",
            "icon_damage", "icon_firerate", "icon_pellets", "icon_pierce", "icon_magazine", "icon_crit", "icon_combo",
            "weapon_pistol", "weapon_rifle", "weapon_shotgun", "weapon_smg", "weapon_sniper", "weapon_laser", "weapon_grenade",
            "ui_panel", "ui_card", "ui_button", "ui_button_down",
        };

        /// <summary>必需音效 key</summary>
        private static readonly string[] AudioKeys =
        {
            "ui_click", "ui_buy",
            "shoot_pistol", "shoot_rifle", "shoot_shotgun", "shoot_smg", "shoot_sniper", "shoot_laser", "shoot_grenade",
            "hit", "kill", "shield_block", "shield_break", "explosion", "gate_hit", "level_clear", "game_over", "combo_up",
        };

        /// <summary>入口：检查并写日志；返回缺失项数量（0 表示清单已齐）</summary>
        [MenuItem("Gunplay/5. 校验素材清单", false, 50)]
        public static void Run()
        {
            List<string> missingRequired = new List<string>();
            List<string> missingOptional = new List<string>();
            List<string> missingAudio = new List<string>();
            int foundRequired = 0;
            int foundOptional = 0;
            int foundAudio = 0;

            foreach (string key in RequiredSpriteKeys)
            {
                if (Resources.Load<Sprite>(ArtLibrarySpriteRoot(key)) != null)
                {
                    foundRequired++;
                }
                else
                {
                    missingRequired.Add(key);
                }
            }

            foreach (string key in OptionalSpriteKeys)
            {
                if (Resources.Load<Sprite>(ArtLibrarySpriteRoot(key)) != null)
                {
                    foundOptional++;
                }
                else
                {
                    missingOptional.Add(key);
                }
            }

            foreach (string key in AudioKeys)
            {
                if (Resources.Load<AudioClip>(ArtLibraryAudioRoot(key)) != null)
                {
                    foundAudio++;
                }
                else
                {
                    missingAudio.Add(key);
                }
            }

            int missingTotal = missingRequired.Count + missingAudio.Count;
            StringBuilder builder = new StringBuilder(2048);
            builder.AppendLine("=== 素材清单校验 ===");
            builder.AppendLine("必需精灵：" + foundRequired + "/" + RequiredSpriteKeys.Length + "，缺失 " + missingRequired.Count);
            if (missingRequired.Count > 0)
            {
                builder.AppendLine("缺失必需精灵：" + string.Join(", ", missingRequired));
            }

            builder.AppendLine("建议精灵：" + foundOptional + "/" + OptionalSpriteKeys.Length + "，缺失 " + missingOptional.Count);
            if (missingOptional.Count > 0)
            {
                builder.AppendLine("缺失建议精灵：" + string.Join(", ", missingOptional));
            }

            builder.AppendLine("音效：" + foundAudio + "/" + AudioKeys.Length + "，缺失 " + missingAudio.Count);
            if (missingAudio.Count > 0)
            {
                builder.AppendLine("缺失音效：" + string.Join(", ", missingAudio));
            }

            builder.AppendLine(missingTotal == 0 ? "ART_CHECK_OK" : "ART_CHECK_MISSING " + missingTotal);
            string text = builder.ToString();
            Debug.Log("[CiArtCheck]" + text);

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogFilePath));
                File.WriteAllText(LogFilePath, text, new UTF8Encoding(false));
            }
            catch (IOException exception)
            {
                Debug.LogWarning("[CiArtCheck] 写日志失败：" + exception.Message);
            }
        }

        /// <summary>拼出精灵的资源路径（与 ArtLibrary.SpriteRoot 保持一致）</summary>
        private static string ArtLibrarySpriteRoot(string key)
        {
            return "Art/Sprites/" + key;
        }

        /// <summary>拼出音频的资源路径（与 ArtLibrary.AudioRoot 保持一致）</summary>
        private static string ArtLibraryAudioRoot(string key)
        {
            return "Art/Audio/" + key;
        }
    }
}
