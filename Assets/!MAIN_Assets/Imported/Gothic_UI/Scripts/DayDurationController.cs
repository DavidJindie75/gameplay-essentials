using UnityEngine;

public class DayDurationController : MonoBehaviour
{

	public GameObject Sky;
	public GameObject Glow;
	[Header("Day Duration in seconds")]
	public float Duration;

	private void Awake ()
	{
		if (Sky == null || Glow == null || Duration <= 0)
		{
			return;
		}
		Sky.GetComponent<Animator>().speed = 1 / Duration;
		Glow.GetComponent<Animator>().speed = 1 / Duration;
	}
}
