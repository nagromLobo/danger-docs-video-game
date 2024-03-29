﻿using UnityEngine;
using System.Collections;


public class Patient : Interactable {
	enum FlashColor 		{NORMAL, BLUE, GREEN, RED, ORANGE}
    enum PatientCriticalState { NORMAL, SPEEDING_UP, ATTACKING, SPEEDING_UP_TO_DIE, ABOUT_TO_DIE, FINISHING, DEAD }

    public float 			bpm;
    public float 			anesthetic_clock_length = 180.0f; //length of time the anesthetic clock is on in seconds

	public Transform  		hotspotSpawnPos;
	public GameObject 		scalpelToolPrefab;
	public GameObject 		sutureToolPrefab;
	public GameObject 		gauzeToolPrefab;
	public GameObject 		scalpelTrackPrefab;
	public GameObject 		sutureHotspotsPrefab;
	public GameObject 		gauzeHotspotsPrefab;

	public bool 			scalpel1Placed;
	public GameObject 		duplicateScalpelTrack;
	public GameObject 		duplicateScalpelSurgeryTool;

	public GameObject[] 	toolSpawnPositions;

	private float 			last_beat_time;
	private float 			next_beat_time;

	private Tool.ToolType 	requiredTool;

    private float criticalCycleDuration = 0.0f;
    private float timeStartCriticalCycle = 0.0f;
    private float timeStartCritialState = 0.0f;
    private float timeToEndCurrentCriticalState = 0.0f;

        // Defibulations needed to stabilize patient
        private int 			defibulationsRemaining;

    // --- Heart rate and patient critical state -- //
    public float normal_bpm = 80.0f;
    public float critical_bpm = 170.0f;
    public float about_to_die_bpm = 210.0f;
    public float hear_rate_modulation_range = 20.0f;
    public float time_to_slow_bpm = 1.0f;

    public GameObject actionButtonCanvas;

    private FlashColor flash_color;
	private PatientCriticalState critical_state;
	private Material NormalStateMaterial;
    private bool tutorialToopPickUp = false;

    private float adverted_bpm;
    //	private float flash_timer = 0.0f;
    //	private float flash_duration = 0.5f;
    //	private float flash_time = 0.0f;


	private static Patient _instance;
	public static Patient Instance {
		get { return _instance; }
	}

	void Awake() {
		if (_instance == null) {
			_instance = this;
		} else {
			Debug.Log("Patient can only be set once");
		}
	}

	//Stitches
	public void OnSuture(float duration)
	{
		var go = (GameObject)Instantiate(sutureHotspotsPrefab, hotspotSpawnPos);
		go.transform.parent = hotspotSpawnPos;
		go.transform.localPosition = Vector3.zero;
		go.transform.localRotation = Quaternion.identity;
		requiredTool = Tool.ToolType.SUTURE;
	}

	public void OnCutPatientOpen(float duration)
	{
		var go = (GameObject)Instantiate(scalpelTrackPrefab, hotspotSpawnPos);
		go.transform.parent = hotspotSpawnPos;
		go.transform.localPosition = Vector3.zero;
		go.transform.localRotation = Quaternion.identity;
		requiredTool = Tool.ToolType.SCALPEL;
	}

	public void OnSoakBlood(float duration)
	{
		var go = (GameObject)Instantiate(gauzeHotspotsPrefab, hotspotSpawnPos);
		go.transform.parent = hotspotSpawnPos;
		go.transform.localPosition = Vector3.zero;
		go.transform.localRotation = Quaternion.identity;
		requiredTool = Tool.ToolType.GAUZE;
	}

	public void OnTutorialSuture()
	{
		Instantiate(sutureHotspotsPrefab, hotspotSpawnPos);
		requiredTool = Tool.ToolType.SUTURE;

	}

	public void OnTutorialScalpelDuplicate()
	{
		Instantiate(duplicateScalpelTrack, hotspotSpawnPos);
		requiredTool = Tool.ToolType.SCALPEL;

	}

	public void OnTutorialScalpel()
	{
		Instantiate(scalpelTrackPrefab, hotspotSpawnPos);
		requiredTool = Tool.ToolType.SCALPEL;
	}
	public void OnTutorialGauze()
	{
		Instantiate(gauzeHotspotsPrefab, hotspotSpawnPos);
		requiredTool = Tool.ToolType.GAUZE;
	}

	// Use this for initialization
	void Start () {
        bpm = normal_bpm;
		LevelUserInterface.UI.UpdateBpm (bpm);
		last_beat_time = Time.time;
        next_beat_time = last_beat_time + bpmToSecondsInterval(bpm);
        DoctorEvents.Instance.onPatientCriticalEventStart += OnPatientCriticalEventStart;
		DoctorEvents.Instance.onPatientCriticalEventEnded += OnPatientCriticalEventEnded;
		DoctorEvents.Instance.patientNeedsStitches += OnSuture;
		DoctorEvents.Instance.patientNeedsCutOpen += OnCutPatientOpen;
		DoctorEvents.Instance.patientNeedsBloodSoak += OnSoakBlood;
        DoctorEvents.Instance.onToolPickedUpForSurgery += OnToolForSurgeryPickedUp;
        DoctorEvents.Instance.onToolDroppedForSurgery += OnToolForSurgeryDropped;
        DoctorEvents.Instance.GameOver += OnPatientDead;

		TutorialEventController.Instance.OnSurgeryOnPatientStart += OnTutorialSuture;
		TutorialEventController.Instance.OnSurgeryOnPatientStart += OnTutorialScalpelDuplicate;
		TutorialEventController.Instance.OnSurgeryOnPatientStart += OnTutorialScalpel;
		TutorialEventController.Instance.OnSurgeryOnPatientStart += OnTutorialGauze;
        TutorialEventController.Instance.OnPickupToolsStart += OnToolPickUpTutorialStart;
        TutorialEventController.Instance.OnPickupToolsEnd += OnToolPickUpTutorialEnd;
        TutorialEventController.Instance.OnToolPickedUp += OnToolPickedUp;
    }

    // Update is called once per frame
    void Update() {
        //print(critical_bpm);
        switch (critical_state) {
            case PatientCriticalState.NORMAL:
                CriticalStateNormalUpdate();
                break;
            case PatientCriticalState.SPEEDING_UP:
                CriticalStateSpeedingUpUpdate();
                break;
            case PatientCriticalState.ATTACKING:
                CriticalStateAttackingUpdate();
                break;
            case PatientCriticalState.SPEEDING_UP_TO_DIE:
                CriticalStateSpeedingUpToDieUpdate();
                break;
            case PatientCriticalState.ABOUT_TO_DIE:
                CriticalStateAboutToDieUpdate();
                break;
            case PatientCriticalState.FINISHING:
                CriticalStateFinishingUpdate();
                break;
        }
        GeneralUpdate();
        // TODO: Add heartbeat message / vitals / things here.
        // EX: renderHeartBeat();
        //			print("Heartbeat Triggered.\nCurrent BPM: " + bpm);

    }


    //  ---- Handle patient critical state -- //
    private void CriticalStateNormalUpdate() {
        // This should be empty?
    }

    private void CriticalStateSpeedingUpUpdate() {
        float t = (Time.time - timeStartCritialState) / (timeToEndCurrentCriticalState - timeStartCritialState);
        float newBpm = Mathf.Lerp(normal_bpm, critical_bpm, t);
        if (t >= 1.0f) {
            bpm = critical_bpm;
            LevelUserInterface.UI.UpdateBpm(bpm);
            critical_state = PatientCriticalState.ATTACKING;
            timeStartCritialState = Time.time;
            print("Heart State Attacking Time: " + Time.time);
            timeToEndCurrentCriticalState = timeStartCritialState + (criticalCycleDuration / 6.0f);
        } else {
            bpm = newBpm;
            LevelUserInterface.UI.UpdateBpm(bpm);
        }
    }

    private void CriticalStateAttackingUpdate() {
        float t = (Time.time - timeStartCritialState) / (timeToEndCurrentCriticalState - timeStartCritialState);
        if (t >= 1.0f) {
            critical_state = PatientCriticalState.SPEEDING_UP_TO_DIE;
            timeStartCritialState = Time.time;
            if (!TutorialEventController.Instance.tutorialActive) {
                DoctorEvents.Instance.InformPatientAboutToDie(criticalCycleDuration - (Time.time - timeStartCriticalCycle));
                print("Heart State Speeding up to die Time: " + Time.time);
                timeToEndCurrentCriticalState = timeStartCritialState + (criticalCycleDuration / 3.0f);
            } else {
                timeToEndCurrentCriticalState = float.MaxValue;
            }
        } 
    }

    private void CriticalStateSpeedingUpToDieUpdate() {
        float t = (Time.time - timeStartCritialState) / (timeToEndCurrentCriticalState - timeStartCritialState);
        float newBpm = Mathf.Lerp(critical_bpm, about_to_die_bpm, t);
        if (t >= 1.0f) {
            bpm = about_to_die_bpm;
            LevelUserInterface.UI.UpdateBpm(bpm);
            print("Heart State About To Die Time: " + Time.time);
            critical_state = PatientCriticalState.ABOUT_TO_DIE;
            timeStartCritialState = Time.time;
            timeToEndCurrentCriticalState = timeStartCritialState + (criticalCycleDuration / 6.0f);
        } else {
            bpm = newBpm;
            LevelUserInterface.UI.UpdateBpm(bpm);
        }
    }

    private void CriticalStateAboutToDieUpdate() {
        if (Time.time >= (timeStartCriticalCycle + criticalCycleDuration)) {
            bpm = 0.0f;
            print("Heart State Dead Time: " + Time.time);
            critical_state = PatientCriticalState.DEAD;
            timeStartCritialState = Time.time;
            LevelUserInterface.UI.UpdateBpm(bpm);
            AudioControl.Instance.PlayHeartMonitorLong();
        }
    }


    private void CriticalStateFinishingUpdate() {
        float t = (Time.time - timeStartCritialState) / (timeToEndCurrentCriticalState - timeStartCritialState);
        float newBpm = Mathf.Lerp(adverted_bpm, normal_bpm, t);
        if (t >= 1.0f) {
            bpm = normal_bpm;
            LevelUserInterface.UI.UpdateBpm(bpm);
            critical_state = PatientCriticalState.NORMAL;
            timeStartCritialState = Time.time;
            print("Heart State Attacking Time: " + Time.time);
        } else {
            bpm = newBpm;
            LevelUserInterface.UI.UpdateBpm(bpm);
        }
    }

    private void GeneralUpdate() {
        // if the time since the last heart beat has passed.
        if (Time.time > next_beat_time) {
            // update last_beat_time
            last_beat_time = Time.time;
            next_beat_time = last_beat_time + bpmToSecondsInterval(bpm);
            float referenceHeartRate = normal_bpm;

            // if in critical state sound heart monitor on each heart beat //
            if (ShouldSoundMonitorBeep(critical_state)) {
                AudioControl.Instance.PlayHeartMonitorBeep();
            }

            // Modulate Heart rate  ///
            switch (critical_state) {
                case PatientCriticalState.NORMAL:
                    referenceHeartRate = normal_bpm;
                    break;
                case PatientCriticalState.SPEEDING_UP:
                    referenceHeartRate = (normal_bpm + critical_bpm) / 2.0f;
                    break;
                case PatientCriticalState.ATTACKING:
                    referenceHeartRate = critical_bpm;
                    break;
                case PatientCriticalState.FINISHING:
                    referenceHeartRate = (normal_bpm + critical_bpm) / 2.0f;
                    break;
            }
            // randomly increment or decrement heart rate within window (add a modulation to the heart rate
            if (critical_state != PatientCriticalState.DEAD) {
                float randomValue = Random.value;
                if (0.333333f > randomValue) {
                    if (bpm > (referenceHeartRate - hear_rate_modulation_range)) {
                        bpm--;
                        LevelUserInterface.UI.UpdateBpm(bpm);
                    }
                } else if (0.666666f < randomValue) {
                    if (bpm < (referenceHeartRate + hear_rate_modulation_range)) {
                        ++bpm;
                        LevelUserInterface.UI.UpdateBpm(bpm);
                    }
                }
            }
        }
    }

    public void OnPatientCriticalEventStart(float duration) {
        critical_state = PatientCriticalState.SPEEDING_UP;
        this.criticalCycleDuration = duration;
        timeStartCriticalCycle = Time.time;
        timeStartCritialState = timeStartCriticalCycle;
        timeToEndCurrentCriticalState = timeStartCritialState + (duration / 3.0f);
        //flash_timer = duration;
        LevelUserInterface.UI.UpdateBpm(bpm);
    }

    public void OnPatientCriticalEventEnded(float duration) {
        critical_state = PatientCriticalState.FINISHING;
        timeStartCritialState = Time.time;
        timeToEndCurrentCriticalState = timeStartCritialState + time_to_slow_bpm;
        adverted_bpm = bpm;
        defibulationsRemaining = 0;
    }

    public void OnPatientDead(float druation) {
        critical_state = PatientCriticalState.DEAD;
        bpm = 0;
        defibulationsRemaining = 0;
    }


	private float bpmToSecondsInterval(float bpm) {
		return (1f / (bpm / 60f));
	}

    // returns new surgery input (transfered control)
	public SurgeryToolInput receiveOperation(Tool tool, int doctorNumber = -1) {
        SurgeryToolInput newInputController = null;
		if (tool == null)
		{
			return newInputController;
		}
		if (tool.GetToolType() == Tool.ToolType.SUTURE)
		{
			//Get Doctor that initiated operation
			GameObject doc = GameObject.Find("Doctor_" + (doctorNumber + 1).ToString());
			if (doc == null)
			{
				Debug.Log("couldn't find doctor!");
			}
			//Disable their input component
			doc.GetComponent<DoctorInputController>().enabled = false;

			GameObject suture = (GameObject)Instantiate(sutureToolPrefab, toolSpawnPositions[0].transform);
            suture.transform.parent = toolSpawnPositions[0].transform;
            suture.transform.localPosition = Vector3.zero;
            suture.transform.parent = null;
            newInputController = suture.GetComponent<SurgeryToolInput>();
			newInputController.playerNum = doctorNumber;
            suture.transform.parent = this.transform;






            Debug.Log("recieving suture operation");
		}
		else if (tool.GetToolType() == Tool.ToolType.SCALPEL)
		{
			//Get Doctor that initiated operation
			GameObject doc = GameObject.Find("Doctor_" + (doctorNumber + 1).ToString());
			if (doc == null)
			{
				Debug.Log("couldn't find doctor!");
			}
			//Disable their input component
			doc.GetComponent<DoctorInputController>().enabled = false;
			



			//Create tool and give control to Doctor
			if (!scalpel1Placed)
			{
				//Create tool and give control to Doctor
				GameObject scalpel = (GameObject)Instantiate(scalpelToolPrefab, toolSpawnPositions[0].transform);
                scalpel.transform.parent = toolSpawnPositions[0].transform;
                scalpel.transform.localPosition = Vector3.zero;
                scalpel.transform.parent = null;
                newInputController = scalpel.GetComponent<SurgeryToolInput>();
				newInputController.playerNum = doctorNumber;
				scalpel1Placed = true;
                scalpel.transform.parent = this.transform;
            }
			else
			{
                if(TutorialEventController.Instance.tutorialActive){
                    GameObject scalpel = (GameObject)Instantiate(duplicateScalpelSurgeryTool, toolSpawnPositions[0].transform);
                    scalpel.transform.parent = toolSpawnPositions[0].transform;
                    scalpel.transform.localPosition = Vector3.zero;
                    scalpel.transform.parent = null;
                    newInputController = scalpel.GetComponent<SurgeryToolInput>();
                    newInputController.playerNum = doctorNumber;
                    scalpel.transform.parent = this.transform;
                }
                else
                {
                    //Create tool and give control to Doctor
                    GameObject scalpel = (GameObject)Instantiate(scalpelToolPrefab, toolSpawnPositions[0].transform);
                    scalpel.transform.parent = toolSpawnPositions[0].transform;
                    scalpel.transform.localPosition = Vector3.zero;
                    scalpel.transform.parent = null;
                    newInputController = scalpel.GetComponent<SurgeryToolInput>();
                    newInputController.playerNum = doctorNumber;
                    scalpel1Placed = true;
                    scalpel.transform.parent = this.transform;
                }
			}


			Debug.Log("recieving scalpel operation");
		}
		else if (tool.GetToolType() == Tool.ToolType.GAUZE)
		{
			//Get Doctor that initiated operation
			GameObject doc = GameObject.Find("Doctor_" + (doctorNumber + 1).ToString());
			if (doc == null)
			{
				Debug.Log("couldn't find doctor!");
			}
			//Disable their input component
			doc.GetComponent<DoctorInputController>().enabled = false;
		
			//Create tool and give control to Doctor
			GameObject gauze = (GameObject)Instantiate(gauzeToolPrefab, toolSpawnPositions[0].transform);
            gauze.transform.parent = toolSpawnPositions[0].transform;
            gauze.transform.localPosition = Vector3.zero;
            gauze.transform.parent = null;
            newInputController = gauze.GetComponent<SurgeryToolInput>();
			newInputController.playerNum = doctorNumber;
            gauze.transform.parent = this.transform;

            Debug.Log("recieving gauze operation");
		}
		else {
			print("defibulationsRemaining: " + defibulationsRemaining);
			if (defibulationsRemaining > 0) {
				if (tool.GetToolType() == Tool.ToolType.DEFIBULATOR) {
					defibulationsRemaining--;
				}
			}

			if (defibulationsRemaining == 0)
			{
				DoctorEvents.Instance.PatientCriticalAdverted();
                if (TutorialEventController.Instance.tutorialActive) {
                    TutorialEventController.Instance.InformHeartAttackAdverted();
                }
			}		
		}
        return newInputController;
	}

	public override bool DocterIniatesInteracting(Doctor interactingDoctor)
	{
		interactingDoctor.currentTool.OnDoctorInitatedInteracting();
		Debug.Log(interactingDoctor.name + " initiated patient interaction.");
        actionButtonCanvas.SetActive(false);
		return true;
	}


	protected override Tool.ToolType RequiredToolType()
	{
		return requiredTool;
	}

    private bool ShouldSoundMonitorBeep( PatientCriticalState state) {
        switch (state) {
            case PatientCriticalState.ABOUT_TO_DIE:
                return true;
            case PatientCriticalState.ATTACKING:
                return true;
            case PatientCriticalState.FINISHING:
                return true;
            case PatientCriticalState.SPEEDING_UP:
                return true;
            case PatientCriticalState.SPEEDING_UP_TO_DIE:
                return true;
            default:
                return false;
        }
    }

    private void OnToolForSurgeryPickedUp(Tool.ToolType type) {
        actionButtonCanvas.SetActive(true);
        actionButtonCanvas.GetComponent<BounceUpAndDown>().initiateBounce();
    }

    private void OnToolForSurgeryDropped(Tool.ToolType type) {
        actionButtonCanvas.SetActive(false);
    }

    private void OnToolPickUpTutorialStart() {
        tutorialToopPickUp = true;
    }

    private void OnToolPickUpTutorialEnd() {
        tutorialToopPickUp = false;
        actionButtonCanvas.SetActive(false);
    }

    private void OnToolPickedUp(Tool.ToolType type, int playerNum) {
        if (tutorialToopPickUp) {
            actionButtonCanvas.SetActive(true);
        }
    }

    void OnDestroy() {
        DoctorEvents.Instance.onPatientCriticalEventStart -= OnPatientCriticalEventStart;
        DoctorEvents.Instance.onPatientCriticalEventEnded -= OnPatientCriticalEventEnded;
        DoctorEvents.Instance.patientNeedsStitches -= OnSuture;
        DoctorEvents.Instance.patientNeedsCutOpen -= OnCutPatientOpen;
        DoctorEvents.Instance.patientNeedsBloodSoak -= OnSoakBlood;
        DoctorEvents.Instance.onToolPickedUpForSurgery -= OnToolForSurgeryPickedUp;
        DoctorEvents.Instance.onToolDroppedForSurgery -= OnToolForSurgeryDropped;
        DoctorEvents.Instance.GameOver -= OnPatientDead;
        TutorialEventController.Instance.OnPickupToolsStart -= OnToolPickUpTutorialStart;
        TutorialEventController.Instance.OnPickupToolsEnd -= OnToolPickUpTutorialEnd;
        TutorialEventController.Instance.OnToolPickedUp -= OnToolPickedUp;
    }
}
