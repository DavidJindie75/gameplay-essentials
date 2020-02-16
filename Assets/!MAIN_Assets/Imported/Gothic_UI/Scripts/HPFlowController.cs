using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class HPFlowController : MonoBehaviour {
	
	private Material _material;
	public Vector2 MainTexOffset;
	public Vector2 SecondTexOffset;

	private void Start ()
	{
		_material = GetComponent<RawImage>().material;
	}

	private void Update () {
		_material.SetTextureOffset("_MainTex", MainTexOffset);
		_material.SetTextureOffset("_SecondTex", SecondTexOffset);
	}

	public void SetValue(float value)
	{
		_material.SetFloat("_FillLevel", value);
	}
}
