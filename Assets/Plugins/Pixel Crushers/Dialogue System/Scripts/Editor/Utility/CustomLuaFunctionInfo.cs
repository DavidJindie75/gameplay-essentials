using System;
using UnityEngine;

namespace PixelCrushers.DialogueSystem
{
    [CreateAssetMenu(menuName = "Pixel Crushers/Dialogue System/Custom Lua Function Info")]
    public class CustomLuaFunctionInfo : ScriptableObject
    {
        [HelpBox("If you want your own custom Lua functions to appear in Conditions and Script dropdowns, add their info to this asset.", HelpBoxMessageType.None)]
        public CustomLuaFunctionInfoRecord[] conditionFunctions;
        public CustomLuaFunctionInfoRecord[] scriptFunctions;
    }

    [Serializable]
    public class CustomLuaFunctionInfoRecord
    {
        public string functionName;
        public CustomLuaParameterType[] parameters;
        public CustomLuaReturnType returnValue = CustomLuaReturnType.None;
    }

    public enum CustomLuaParameterType { Bool, Double, String, Actor, Quest, QuestEntry, Variable, None }
    public enum CustomLuaReturnType { None, Bool, Double, String }
}