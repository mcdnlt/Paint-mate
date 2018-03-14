using UnityEngine;
using System.Collections;

public class DemoGUI_ColorPickerDemo : MonoBehaviour {
	
	// Use this for initialization
	void Start () {
	}
	
	// Update is called once per frame
	void OnGUI () {
		GUIStyle style = new GUIStyle(GUI.skin.label);
		style.fontSize = 12;
		//style.font = GUI.skin.button.font;
		//style.font.material.color = Color.white;

	}
}
