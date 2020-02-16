/*
This script and the related scene "Tutorial collision vs trigger" explain the differences between collision and trigger.
Generally if you require an accurate point of contact between the 2 colliders, 
you would need OnCollisionEnter which provides the ContactPoint natively. 
As this requires the physics engine, the more collisions happen in the same time, 
the greater possibility that some collision messages would be missed. 
This can be improved by adjusting "Fixed Timestep" (Edit -> Project Settings -> Time) at the expense of overall performance. 

Trigger is the cheaper option which does not require physics engine. 
The point of contact is artificially provided by the engine user, i.e. you. 
It can be either an offset of the weapon or the target. 
Trigger mode is much more popular in many commercial games because of its predictability and performance. 
*/
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CollisionTrigger : MonoBehaviour {
	public Transform onHitEffect;//Hit vfx prefab
	private Transform onHitClone;//Prefab instantiated to the scene as a clone
	private ParticleSystem onHitPS;//Particle System component of the hit vfx
	private AudioSource onHitAudio;//Audio Source component of the hit sfx
	
	void Awake () {
		onHitClone = Instantiate(onHitEffect);//instnatiate the hit vfx into the scene
		onHitClone.gameObject.name = this.gameObject.name+": "+onHitClone.gameObject.name;//Rename the Game Object name of the clone
		onHitPS = onHitClone.GetComponent<ParticleSystem>();//Get the Particle System component from the clone
		onHitAudio = onHitClone.GetComponent<AudioSource>();//Get the Audio Source component from the clone
	}
	
	//Collision mode
	void OnCollisionEnter(Collision collision) {
		foreach (ContactPoint contact in collision.contacts) {
			onHitClone.position = contact.point;//Re-position the hit vfx to the point of contact
			
			InitiateOnHitActions();
			
			Debug.Log(contact.thisCollider.name + " hit " + contact.otherCollider.name);//Return a debug message in the Console
		}
	}
	
	//Trigger mode
	void OnTriggerEnter(Collider other) {
		onHitClone.position = this.transform.position + new Vector3(0f, 0f, 0.4f);//Hit clone re-positioned to the weapon z-axis +0.4
		//onHitClone.position = other.transform.position;//Hit clone re-positioned to the target (collider) center
		
		InitiateOnHitActions();
		
		Debug.Log("Trigger Entered "+other);
	}
	
	void InitiateOnHitActions() {
		onHitPS.Stop();//Clean up the previous hit vfx particles
		onHitPS.Play();//Play the hit vfx again
		
		onHitAudio.Stop();//Clean up the previous hit sfx
		onHitAudio.volume = Random.Range(0.85f, 1.15f);//Randomize the hit sfx volume
		onHitAudio.pitch = Random.Range(0.9f, 1.1f);//Randomize the hit sfx pitch
		onHitAudio.Play();//Play the hit sfx again
	}
}