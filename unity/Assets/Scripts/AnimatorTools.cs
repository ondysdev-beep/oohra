using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Pomůcky pro Animator vlastních modelů (katalog vzhledu). Hra posílá hodnoty jen do parametrů,
    /// které model opravdu má, takže model bez animací nebo s jinými parametry nic nerozbije.
    /// </summary>
    public static class AnimatorTools
    {
        public static bool Has(Animator animator, string name, AnimatorControllerParameterType type)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return false;
            }

            foreach (var parameter in animator.parameters)
            {
                if (parameter.name == name && parameter.type == type)
                {
                    return true;
                }
            }

            return false;
        }

        public static void SetInt(Animator animator, string name, int value)
        {
            if (Has(animator, name, AnimatorControllerParameterType.Int)) animator.SetInteger(name, value);
        }

        public static void SetFloat(Animator animator, string name, float value)
        {
            if (Has(animator, name, AnimatorControllerParameterType.Float)) animator.SetFloat(name, value);
        }

        public static void SetBool(Animator animator, string name, bool value)
        {
            if (Has(animator, name, AnimatorControllerParameterType.Bool)) animator.SetBool(name, value);
        }
    }
}
