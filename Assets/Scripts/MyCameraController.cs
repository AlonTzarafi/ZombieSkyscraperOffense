using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;


public class MyCameraController : MonoBehaviour
{
    [SerializeField] private float dodgeDuration;
    [SerializeField] private float dodgePower;
    [SerializeField] private float dodgeCooldownTotalDuration;
    
    public Transform cameraPresetPositions;

    public float zombieSpeed;
    public float flyThroughSpeed;
    public float flyThroughRotationSpeed;

    [HideInInspector] public bool isAZombie;


    private float zombieMinY;
    private float zombieMinZ;
    private bool noZombiesLeft;
    
    private float dodgeCooldown;

    private Transform buildingTransform;

    private float dodgeTimeLeft;

    private bool turnedIntoZombieOnGameStart;

    // Start is called before the first frame update
    void Start()
    {
        zombieMinY = transform.position.y;
        zombieMinZ = transform.position.z;
    }

    // Update is called once per frame
    void Update()
    {
        if (!turnedIntoZombieOnGameStart) {
            
            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            var allZombieEntitiesQuery = entityManager.CreateEntityQuery(typeof(ZombieData), typeof(LocalToWorld));
            var allZombieEntitiesLength = allZombieEntitiesQuery.CalculateEntityCount();
            if (allZombieEntitiesLength > 0) {
                TurnIntoZombie();
                turnedIntoZombieOnGameStart = true;
            }
            return;
        }
        
        if (buildingTransform == null) {
            buildingTransform = GameObject.Find("Building").transform;
        }

        
        var pressedNum = 0;

        if (Input.GetKeyDown(KeyCode.X)) {pressedNum = 1;}
        if (Input.GetKeyDown(KeyCode.C)) {pressedNum = 2;}
        if (Input.GetKeyDown(KeyCode.V)) {pressedNum = 3;}
        if (Input.GetKeyDown(KeyCode.B)) {pressedNum = 4;}

        if (pressedNum > 0) {
            TurnIntoCamera(pressedNum);
        } else {
            if (Input.GetKeyDown(KeyCode.Z) && !isAZombie) {
                TurnIntoZombie();
            }
        }

        if (isAZombie) {
            ControlZombie();
            GUITest.bottomLeftText = "Press SPACE to dodge. Press X,C,V,B for flythrough camera";
        } else {
            ControlFlythroughCamera();
            GUITest.bottomLeftText = "Press Z to play as a zombie";
            if (noZombiesLeft) {
                GUITest.bottomLeftText = "No zombies left";
            }
        }
    }

    public void TurnIntoCamera(int cameraNum = 1)
    {
        var cameraPresetPositionsChildren = cameraPresetPositions.GetComponentsInChildren<Transform>();
        var cameraPresetPosition = cameraPresetPositionsChildren[cameraNum];
        transform.position = cameraPresetPosition.position;
        transform.rotation = cameraPresetPosition.rotation;
        isAZombie = false;
    }

    public void TurnIntoZombie()
    {
        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        var allZombieEntitiesQuery = entityManager.CreateEntityQuery(typeof(ZombieData), typeof(LocalToWorld));
        var allZombieEntitiesLength = allZombieEntitiesQuery.CalculateEntityCount();

        if (allZombieEntitiesLength > 0) {
            var allZombiesEntities = allZombieEntitiesQuery.ToEntityArray(Allocator.Temp);
            var allZombiesLocalTransforms = allZombieEntitiesQuery.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
            var allZombiesToZValues = new Dictionary<Entity, float>(allZombieEntitiesLength);
            for (int i = 0; i < allZombieEntitiesLength; i++) {
                var localTransform = allZombiesLocalTransforms[i];
                allZombiesToZValues.Add(allZombiesEntities[i], localTransform.Position.z);
            }

            var sortedTupleArray = allZombiesToZValues.OrderBy(z => z.Value).ToArray();

            var middle10PercentBeginIndex = Mathf.FloorToInt(sortedTupleArray.Length * 0.45f);
            var middle10PercentEndIndex = Mathf.CeilToInt(sortedTupleArray.Length * 0.55f);

            if (middle10PercentBeginIndex >= middle10PercentEndIndex) {
                // If reach any problem, just take all
                middle10PercentBeginIndex = 0;
                middle10PercentEndIndex = sortedTupleArray.Length;
            }

            var middle10Percent = new Entity[middle10PercentEndIndex - middle10PercentBeginIndex];

            for (int i = middle10PercentBeginIndex; i < middle10PercentEndIndex; i++) {
                var arrayIndex = i - middle10PercentBeginIndex;
                if (arrayIndex > 0 && arrayIndex < middle10Percent.Length) {
                    middle10Percent[arrayIndex] = sortedTupleArray[i].Key;
                }
            }

            var randomIndex = UnityEngine.Random.Range(0, middle10Percent.Length);
            var zombieEntity = middle10Percent[randomIndex];


            if (!entityManager.HasComponent<LocalToWorld>(zombieEntity)) {
                //Fallback
                var allZombieEntitiesRandomIndex = UnityEngine.Random.Range(0, allZombieEntitiesLength);
                zombieEntity = sortedTupleArray[allZombieEntitiesRandomIndex].Key;
            }
            var zombiePosition = entityManager.GetComponentData<LocalTransform>(zombieEntity).Position;

            // Set camera to this zombie. Set camera to zombie controls
            transform.position = new Vector3(zombiePosition.x, ZombieSpawnerSystem.zombieHeight, zombiePosition.z);
            isAZombie = true;

            // Kill that zombie
            entityManager.DestroyEntity(zombieEntity);

            // Reset dodge cooldown
            dodgeCooldown = 0f;
        } else {
            noZombiesLeft = true;
            TurnIntoCamera(1);
        }
    }

    private void ControlZombie()
    {
        // Update dodge cooldown
        {
            dodgeCooldown += Time.deltaTime / dodgeCooldownTotalDuration;
            dodgeCooldown = Mathf.Clamp(dodgeCooldown, 0, 1);

            GUITest.dodgeCooldown = dodgeCooldown;
        }
        
        var mousePosition = Input.mousePosition;
        var mousePosition0To1 = new Vector2(mousePosition.x / Screen.width, mousePosition.y / Screen.height);
        var mousePositionNormalized = new Vector2(mousePosition0To1.x * 2 - 1, mousePosition0To1.y * 2 - 1);


        transform.eulerAngles = new Vector3(100 * -mousePositionNormalized.y, 100 * mousePositionNormalized.x, 0);

        // float h = horizontalLookSpeed * Input.GetAxis("Mouse X");
        // float v = verticalLookSpeed * Input.GetAxis("Mouse Y");
        // transform.Rotate(v, h, 0);


        var controlDirection = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));

        var cameraForward = transform.forward;
        cameraForward.y = 0;
        cameraForward = math.normalize(cameraForward);
        var cameraRight = transform.right;
        cameraRight.y = 0;
        cameraRight = math.normalize(cameraRight);




        var totalDirection = cameraForward * controlDirection.z + cameraRight * controlDirection.x;
        transform.position += totalDirection * zombieSpeed * Time.deltaTime;

        // transform.position += controlDirection * (Time.deltaTime * speed);

        
        if (dodgeCooldown >= 1f) {
            if (Input.GetKeyDown(KeyCode.Space)) {
                dodgeTimeLeft = dodgeDuration;
                dodgeCooldown = 0f;
            }
        }

        if (dodgeTimeLeft > 0f) {
            dodgeTimeLeft -= Time.deltaTime;
            var dodgeCurrentPower = Mathf.Lerp(dodgePower, 0, dodgeTimeLeft / dodgeDuration);
            var dodgeDirection = totalDirection;
            if (dodgeDirection == Vector3.zero) {
                dodgeDirection = cameraForward;
            }
            transform.position += dodgeDirection * dodgeCurrentPower * Time.deltaTime;
        }

        


        var newY = Mathf.Max(transform.position.y, zombieMinY);
        var newZ = Mathf.Max(transform.position.z, zombieMinZ);
        transform.position = new Vector3(transform.position.x, newY, newZ);
    }

    private void ControlFlythroughCamera()
    {
        // No dodge in flythrough camera
        GUITest.dodgeCooldown = -1f;
        
        var mousePosition = Input.mousePosition;
        var mousePosition0To1 = new Vector2(mousePosition.x / Screen.width, mousePosition.y / Screen.height);
        var mousePositionNormalized = new Vector2(mousePosition0To1.x * 2 - 1, mousePosition0To1.y * 2 - 1);


        var val = flyThroughRotationSpeed;
        transform.eulerAngles += new Vector3(val * -mousePositionNormalized.y, val * mousePositionNormalized.x, 0);

        var controlDirection = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));

        var cameraForward = transform.forward;
        cameraForward = math.normalize(cameraForward);
        var cameraRight = transform.right;
        cameraRight.y = 0;
        cameraRight = math.normalize(cameraRight);



        transform.position += (cameraForward * controlDirection.z + cameraRight * controlDirection.x) * flyThroughSpeed * Time.deltaTime;

        var flyThroughMinY = 4f;
        var newY = Mathf.Max(transform.position.y, flyThroughMinY);
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);

        // Ensure camera doesn't go into the building
        {
            var buildingCenter = buildingTransform.position;
            var buildingSize = buildingTransform.localScale;

            var toBuildingCenter = buildingCenter - transform.position;

            var cameraInsideBuilding = false;
            if (Mathf.Abs(toBuildingCenter.x) < buildingSize.x / 2) {
                if (Mathf.Abs(toBuildingCenter.y) < buildingSize.y / 2) {
                    if (Mathf.Abs(toBuildingCenter.z) < buildingSize.z / 2) {
                        cameraInsideBuilding = true;
                    }
                }
            }

            // If camera is inside building, move it out
            if (cameraInsideBuilding) {
                var toMoveAwayFromBuilding = -toBuildingCenter;
                toMoveAwayFromBuilding.y = 0;
                toMoveAwayFromBuilding = math.normalize(toMoveAwayFromBuilding);
                transform.position += toMoveAwayFromBuilding * (2f * flyThroughSpeed) * Time.deltaTime;
            }
        }
    }
}
