using UnityEngine;

public class GoopyLeGrande_Death : StateMachineBehaviour
{
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // 摇晃相机
        CameraShake.Instance.ShakeCamera(1f, 0.2f);

        // 播放死亡音效
        GoopyLeGrande goopy = animator.GetComponent<GoopyLeGrande>();
        if (goopy.currentPhase == GoopyLeGrande.Phase.Phase1)
        {
            goopy.playDeath1Sound();
            goopy.StopPunch();

            // 切换到第二阶段
            goopy.EnterPhase2();
        }
        else
        {
            goopy.playDeath2Sound();
            goopy.StopPunch();
        }
    }
}