/*
 * 
 * Author: dtobi
 * This work is shared under CC BY-NC-SA 3.0 license.
 * Non commercial, derivatives allowed, attribution if shared unmodified
 * 
 * Note: please note that the code in this module has a different license than all the other KM
 * modules.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using km_Lib;
using KSP.IO;

namespace KM_Lib
{

	/* Code borrowed from the firespitter pack. All credits for the buoyancy model go to:
	 * Firespitter Plane parts and Helicopter Rotors by Snjo. agogstad@gmail.com
     * 
     * 
	 */
    public class KMbuoyancy : PartModule
    {

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = false, guiName = "Force")]
        public float buoyancyForce = 12f; // the force applied to lift the part, scaled by depth according to buoyancyRange
        [KSPField]
        public float buoyancyRange = 1f; // the max depth at which the buoyancy will be scaled up. at this depth, the force applied is equal to buiyoancyForce. At 0 depth, the force is 0
        [KSPField]
        public float buoyancyVerticalOffset = 0.05f; // how high the part rides on the water in meters. Not a position offset inside the part. This will be applied in the global axis regardless of part rotation. Think iceberg/styrofoam.
        [KSPField]
        public float maxVerticalSpeed = 0.2f; // the max speed vertical speed at which there will be a lifitng force applied. Reduces bobbing.
        [KSPField]
        public float dragInWater = 1.5f; // when in water, apply drag to slow the craft down. Stock drag is 3.
        [KSPField]
        public bool debugMode = false;
        [KSPField]
        public float waterImpactTolerance = 125f;
        [KSPField]
        public string forcePointName; // if defined, this is the point that's checked for height, and where the force is applied. allows for several modules on one part through use of many named forecePoints. If undefined, uses part.transform
        [KSPField]
        public bool splashFXEnabled = true;
        [KSPField]
        public string sound_inflate = "";
        [KSPField]
        public string sound_deflate = "";


        [KSPField(isPersistant = true, guiName = "Inflated")] // remember if the part is inflated
        public bool isInflated = false;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "ForceInflated")]
        public float buoyancyForceInflated = 12f;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Master")]
        public bool isMaster = true;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Auto Deploy"),
            UI_Toggle(disabledText="Disabled", enabledText="Enabled")]
        public bool autoDeploy = true;


        public Transform forcePoint;
        public float buoyancyIncrements = 1f; // using the events, increase or decrease buoyancyForce by this amount
        //private float defaultMinDrag;
        //private float defaultMaxDrag;
        public bool splashed;
        private float splashTimer = 0f;
        public float splashCooldown = 0.5f;

        [KSPEvent(guiActive = false,  guiActiveEditor = false, guiName = "increase buoyancy")]
        public void increaseBuoyancyEvent()
        {
            buoyancyForceInflated += buoyancyIncrements;
            updateChildren();
            Debug.Log("buoyancyForceInflated: " + buoyancyForce);
        }

        [KSPEvent(guiActive = false, guiName = "decrease buoyancy")]
        public void decreaseBuoyancyEvent()
        {
            buoyancyForceInflated -= buoyancyIncrements;
            updateChildren();
            Debug.Log("buoyancyForceInflated: " + buoyancyForce);
        }

        public override void OnStart(PartModule.StartState state)
        {
            base.OnStart(state);

            // Check if the part is already inflated
            if (isInflated) {
                buoyancyForce = buoyancyForceInflated;
            } else {
                buoyancyForce = 0f;
            }
            if (isMaster) {
                print ("DB11\n");
                Utility.playAnimation (this.part, "inflate", isInflated, false, 1.0f);
                print ("DB12\n");
                if(Events ["toggleInflate"] != null)            Events ["toggleInflate"].guiActive          = true;
                if(Events ["toggleInflate"] != null)            Events ["toggleInflate"].guiActiveEditor    = true;
                if(Events ["toggleAutoDeploy"] != null)         Events ["toggleAutoDeploy"].guiActive       = true;
                if(Events ["toggleAutoDeploy"] != null)         Events ["toggleAutoDeploy"].guiActiveEditor = true;
                if(Fields ["autoDeploy"] != null)               Fields ["autoDeploy"].guiActive             = true;
                if(Fields ["autoDeploy"] != null)               Fields ["autoDeploy"].guiActiveEditor       = true;
                if(Fields ["buoyancyForceInflated"] != null)    Fields ["buoyancyForceInflated"].guiActive  = true;
            } else {
                print ("DB21\n");
                Actions.Remove (Actions ["toggleInflateAG"]);
                Actions.Remove (Actions ["toggleAutoDeployAG"]);
                print ("DB22\n");
            }



            print ("DB31\n");
            //defaultMinDrag = part.minimum_drag;
            //defaultMaxDrag = part.maximum_drag;
            if (forcePointName != string.Empty)
            {
                forcePoint = part.FindModelTransform(forcePointName);
            }
            if (forcePointName == string.Empty || forcePoint == null)
            {
                forcePoint = part.transform;
            }
            print ("DB32\n");
            if (isMaster && debugMode)
            {   Fields ["isInflated"].guiActive = true;
                Fields ["ForceInflated"].guiActive = true;
                Events["increaseBuoyancyEvent"].guiActive = true;
                Events["decreaseBuoyancyEvent"].guiActive = true;

                if (forcePointName != string.Empty)
                {
                    Events["increaseBuoyancyEvent"].guiName = "increase buoy " + forcePointName;
                    Events["decreaseBuoyancyEvent"].guiName = "decrease buoy " + forcePointName;
                }
            }
            print ("DB33\n");
        }

        [KSPAction("Inflate / Deflate")]
        public void toggleInflateAG (KSPActionParam param){
            toggleInflate ();
        }

        [KSPEvent(guiName = "Inflate / Deflate", guiActive = false, guiActiveEditor = false)]
        public void toggleInflate ()
        {
            if(isMaster){
                if (isInflated) {
                    buoyancyForce = 0f;
                    Utility.playAnimation (this.part, "inflate", false, true, 1f);
                    Utility.playAudio(this.part, sound_deflate);
                    autoDeploy = false;
                } else {
                    buoyancyForce = buoyancyForceInflated;
                    Utility.playAnimation (this.part, "inflate",  true, true, 2.5f);
                    Utility.playAudio(this.part, sound_inflate);
                    autoDeploy = false;
                }
                isInflated = !isInflated;
                updateChildren();
            }

        }

        [KSPAction("Toggle Auto deploy")]
        public void toggleAutoDeployAG (KSPActionParam param){
            autoDeploy = !autoDeploy;
        }

        private void updateChildren(){
            if (isMaster) {
                KMbuoyancy[] bParts = this.part.GetComponents<KMbuoyancy>();
                if(bParts != null){
                    foreach (KMbuoyancy bPart in bParts) {
                        if (!bPart.isMaster) {
                            bPart.buoyancyForce = buoyancyForce;
                            bPart.buoyancyForceInflated = buoyancyForceInflated;
                            bPart.isInflated = isInflated;
                            print ("Updated Child" + bPart.forcePointName);
                        }
                    }
                }
            }

        }

        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight)
                return;
            print ("DB41\n");
            if (this.vessel.Splashed && isMaster && autoDeploy && !isInflated) {
                toggleInflate ();
            }
            print ("DB42\n");
            if (vessel.mainBody.ocean && part.rigidbody != null)
            {
                if (part.partBuoyancy != null)
                {
                    Destroy(part.partBuoyancy);
                }


                float partAltitude = Vector3.Distance(forcePoint.position, vessel.mainBody.position) - (float)vessel.mainBody.Radius - buoyancyVerticalOffset;
                if (partAltitude < 0f)
                {
                    // float code

                    float floatMultiplier = Mathf.Max(0f, -Mathf.Max((float)partAltitude, -buoyancyRange)) / buoyancyRange;

                    if (floatMultiplier > 0f)
                    {
                        Vector3 up = (this.vessel.rigidbody.position - this.vessel.mainBody.position).normalized;
                        Vector3 uplift = up * buoyancyForce * floatMultiplier;

                        //float relativeDirection = Vector3.Dot(vessel.rigidbody.velocity.normalized, up);                        

                        if (vessel.verticalSpeed < maxVerticalSpeed) // || relativeDirection < 0f) // if you are going down, apply force regardless, of going up, limit up speed
                        {
                            this.part.rigidbody.AddForceAtPosition(uplift, forcePoint.position);
                        }
                    }

                    // set water drag

                    part.rigidbody.drag = dragInWater;

                    // splashed status

                    splashed = true;
                    part.WaterContact = true;
                    part.vessel.Splashed = true;

                    // part destruction

                    if (base.rigidbody.velocity.magnitude > waterImpactTolerance)
                    {                               
                        GameEvents.onCrashSplashdown.Fire(new EventReport(FlightEvents.SPLASHDOWN_CRASH, this.part, this.part.partInfo.title, "ocean", 0, "FSbuoyancy: Hit the water too hard"));
                        this.part.Die();
                        return;
                    }

                    //FX                

                    if (splashFXEnabled)
                    {
                        splashTimer -= Time.deltaTime;
                        if (splashTimer <= 0f)
                        {
                            splashTimer = splashCooldown;
                            if (base.rigidbody.velocity.magnitude > 6f && partAltitude > -buoyancyRange) // don't splash if you are deep in the water or going slow
                            {
                                if (Vector3.Distance(base.transform.position, FlightGlobals.camera_position) < 500f)
                                {
                                    FXMonger.Splash(base.transform.position, base.rigidbody.velocity.magnitude / 50f);
                                }
                            }
                        }
                    }

                }
                else
                {
                    if (splashed)
                    {
                        splashed = false;

                        // set air drag
                        part.rigidbody.drag = 0f;

                        part.WaterContact = false;
                        part.vessel.checkSplashed();
                    }
                }
            }
        }
    }
}