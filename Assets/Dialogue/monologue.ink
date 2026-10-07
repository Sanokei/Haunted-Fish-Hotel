
EXTERNAL InputText(question,key,useProfile)

EXTERNAL Emoji(emoteName,characterTag)

// int, string, string, string, string
// color should be a hex color e.g FFFFFF 
// EXTERNAL CreateQuest(id, questName, questBody, iconName, color)

EXTERNAL MoveTo(characterTag, x, y, delay, disappear)

EXTERNAL ChangeScene(sceneName)

EXTERNAL SetCamera(camName)

// UI/cutscene story functions. Listeners use object tags, like MoveTo and SetCamera.
EXTERNAL SetVisible(objectTag, visible)
EXTERNAL SetVariant(objectTag, variantTag)
EXTERNAL PushTo(panelTag, seconds)
EXTERNAL MoveUI(objectTag, x, y, seconds)
EXTERNAL Knock(objectTag, count, interval)
EXTERNAL Wait(seconds)
EXTERNAL ExpandPanel(panelTag, seconds)
EXTERNAL FlashFrames(frames)
EXTERNAL DollyTo(sceneTag, amount, seconds)
EXTERNAL CreateUI(prefabTag, instanceTag, parentTag)
