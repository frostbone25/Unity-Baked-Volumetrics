using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public static class VolumeGeneratorUtillity
{
    public static void UpdateProgressBar(string description, float progress) => EditorUtility.DisplayProgressBar("Baked Volumetrics", description, progress);

    public static void CloseProgressBar() => EditorUtility.ClearProgressBar();
}
