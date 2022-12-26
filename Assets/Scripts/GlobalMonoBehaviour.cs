using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Entities;
using UnityEngine;

public class GlobalMonoBehaviour : MonoBehaviour
{
    public float projectileScale;
    public float timeBetweenProjectileThrows;
    public float projectileYAcceleration;
    public float projectileYMinVel;
    [Range(0, 1)] public float microwaveChance;
    public float explosionScale;
    public int populationLossPerZombie;
    public int populationLossPerPlayerZombie;
    public TextMeshPro populationText;
    public TextMeshPro zombieScannerText;
}
