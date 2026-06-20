using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SpektraGames.BuildAutomation.Editor;
using SpektraGames.BuildAutomation.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace EditorScript
{
    public static class ProjectSpecificBuildCheckItems
    {
        [AddCheckItemsToBuildChecker(executeOrder: 1)]
        public static List<BuildCheckItem> BuildCheckItems()
        {
            List<BuildCheckItem> result = new List<BuildCheckItem>();

            result.Add(new BuildCheckItem()
            {
                text = "Incremental GC",
                showThisTextIfNotResolved = "Currently disabled",
                IsResolved = PlayerSettings.gcIncremental == true,
                QuickOptions = new BuildCheckItem.QuickOptionsClass(false, true),
                resolveAction = () => { PlayerSettings.gcIncremental = true; },
                oppositeAction = () => { PlayerSettings.gcIncremental = false; }
            });

            // result.Add(new BuildCheckItem()
            // {
            //     text = "Gadsme Sandbox should be disabled",
            //     showThisTextIfNotResolved = "Currently enabled",
            //     IsResolved = GadsmePreferences.P23.forceSandbox == false,
            //     QuickOptions = new BuildCheckItem.QuickOptionsClass(null, true),
            //     resolveAction = () =>
            //     {
            //         GadsmePreferences.P23.forceSandbox = false;
            //         EditorUtility.SetDirty(GadsmePreferences.P23);
            //     },
            //     oppositeAction = () =>
            //     {
            //         GadsmePreferences.P23.forceSandbox = true;
            //         EditorUtility.SetDirty(GadsmePreferences.P23);
            //     }
            // });

            result.Add(new BuildCheckItem()
            {
                text = "Shader Compile Logs should be disabled",
                showThisTextIfNotResolved = "Currently enabled",
                IsResolved = !GraphicsSettings.logWhenShaderIsCompiled,
                QuickOptions = new BuildCheckItem.QuickOptionsClass(null, true),
                resolveAction = () =>
                {
                    GraphicsSettings.logWhenShaderIsCompiled = false;
                    EditorUtility.SetDirty(GraphicsSettings.GetGraphicsSettings());
                },
                oppositeAction = () =>
                {
                    GraphicsSettings.logWhenShaderIsCompiled = true;
                    EditorUtility.SetDirty(GraphicsSettings.GetGraphicsSettings());
                }
            });

            result.Add(new BuildCheckItem()
            {
                text = "Log Levels should be None",
                showThisTextIfNotResolved = "Currently Full or ScriptOnly",
                IsResolved =
                    PlayerSettings.GetStackTraceLogType(LogType.Log) == StackTraceLogType.None &&
                    PlayerSettings.GetStackTraceLogType(LogType.Warning) == StackTraceLogType.None &&
                    PlayerSettings.GetStackTraceLogType(LogType.Error) == StackTraceLogType.ScriptOnly &&
                    PlayerSettings.GetStackTraceLogType(LogType.Assert) == StackTraceLogType.ScriptOnly &&
                    PlayerSettings.GetStackTraceLogType(LogType.Exception) == StackTraceLogType.ScriptOnly,
                QuickOptions = new BuildCheckItem.QuickOptionsClass(false, true),
                //QuickOptions = null,
                resolveAction = () =>
                {
                    PlayerSettings.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
                    PlayerSettings.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
                    PlayerSettings.SetStackTraceLogType(LogType.Error, StackTraceLogType.ScriptOnly);
                    PlayerSettings.SetStackTraceLogType(LogType.Assert, StackTraceLogType.ScriptOnly);
                    PlayerSettings.SetStackTraceLogType(LogType.Exception, StackTraceLogType.ScriptOnly);
                },
                oppositeAction = () =>
                {
                    PlayerSettings.SetStackTraceLogType(LogType.Log, StackTraceLogType.ScriptOnly);
                    PlayerSettings.SetStackTraceLogType(LogType.Warning, StackTraceLogType.ScriptOnly);
                    PlayerSettings.SetStackTraceLogType(LogType.Error, StackTraceLogType.ScriptOnly);
                    PlayerSettings.SetStackTraceLogType(LogType.Assert, StackTraceLogType.ScriptOnly);
                    PlayerSettings.SetStackTraceLogType(LogType.Exception, StackTraceLogType.ScriptOnly);
                }
            });

            return result;
        }
    }
}