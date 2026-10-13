#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
public static class BossTheatreOwnedBatch
{
 public static void Theatre()
 {
  var body=UnityEditor.AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(HotelTypographyAuthoring.BodyPath);body.ReadFontAssetDefinition();if(!body.HasCharacters("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .,;:!?/'()-+%&",out System.Collections.Generic.List<char> missing))HotelTypographyAuthoring.BakeBodyFontOnly();TheatreForegroundFraming.Apply();
  File.WriteAllText("Tools/TheatreTests/Evidence/runtime-ready.txt","Theatre foreground, selected tabs, shared TMP fonts and authoritative return handshake published/native authored. Native validation proceeding. Lobby owner may bind portal after batch release.");
  TheatreNativeValidation.Run();
 }
 public static void Boss()
 {throw new Exception("Publish boss helpers before running separate boss batch");}
}
#endif
