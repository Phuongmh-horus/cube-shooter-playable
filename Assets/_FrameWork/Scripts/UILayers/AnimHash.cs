using UnityEngine;

public static class AnimHash
{

    //PEA ANIM
    public static readonly int PEA_IDLE = Animator.StringToHash("Pea_Idle");
    public static readonly int PEA_BUNNY = Animator.StringToHash("Pea_bunny");
    public static readonly int PEA_JUMP = Animator.StringToHash("Pea_jump");

    public static readonly string PEA_IDLE_STR = "Pea_Idle";
    public static readonly string PEA_BUNNY_STR = "Pea_bunny";
    public static readonly string PEA_JUMP_STR = "Pea_jump";

    //SHOOTER ANIM
    public static readonly int SHOOTER_IDLE = Animator.StringToHash("Shooter_Idle");
    public static readonly int SHOOTER_APPEAR = Animator.StringToHash("Shooter_appear");
    public static readonly int SHOOTER_SHOOT = Animator.StringToHash("Shooter_shoot");
}