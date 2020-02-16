using UnityEngine;

[RequireComponent(typeof(CanvasRenderer))]
public class Liquid : MonoBehaviour
{

	public Mesh Mesh;
	public Material Material;
	public Vector2 MainTexOffset;
	public Vector2 SecondTexOffset;
	public Vector2 ThirdTexOffset;
	private Material _materialInstance;

	private void Start ()
	{
		var canvasRenderer = GetComponent<CanvasRenderer>();
		canvasRenderer.SetMesh(Mesh);
		_materialInstance = Material;
		canvasRenderer.SetMaterial(_materialInstance, null);
	}

	private void Update()
	{
		_materialInstance.SetTextureOffset("_MainTex", MainTexOffset);
		_materialInstance.SetTextureOffset("_SecondTex", SecondTexOffset);
		_materialInstance.SetTextureOffset("_ThirdTex", ThirdTexOffset);
	}

	public void SetValue(float value)
	{
		_materialInstance.SetFloat("_FillLevel", value);
	}

}
