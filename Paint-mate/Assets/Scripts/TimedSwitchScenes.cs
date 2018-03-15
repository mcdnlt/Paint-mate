using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using HoloToolkit.Examples.Prototyping;

public class TimedSwitchScenes : MonoBehaviour {

	public float delay;

	// Use this for initialization
	void Start()
	{
		StartCoroutine(LoadSceneAfterDelay());     
	}

	IEnumerator LoadSceneAfterDelay()
	{
		yield return new WaitForSeconds(this.delay);
		SceneSwitcher switcher = new SceneSwitcher();
		switcher.NextScene();
	}
	
	// Update is called once per frame
	void Update () {
		
	}
}
