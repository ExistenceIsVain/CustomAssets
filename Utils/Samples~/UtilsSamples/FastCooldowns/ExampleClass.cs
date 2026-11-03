using System;
using UnityEngine;
using Utils.Cooldowns;

namespace Utils.Samples.FastCooldowns
{
    /// <summary>
    /// Оружие без MonoBehaviour. Ключ с владельцем (this): у каждого экземпляра свой кулдаун
    /// "attack", два меча не делят одну перезарядку. Dispose снимает все ключи владельца —
    /// иначе статический словарь FastCooldown держал бы мёртвое оружие.
    /// </summary>
    public class ExampleClass : IDisposable
    {
        private readonly CooldownKey attackKey;
        private readonly float attackCooldown;

        public ExampleClass(string name, float attackCooldown)
        {
            Name = name;
            this.attackCooldown = attackCooldown;
            attackKey = new CooldownKey(this, "attack");
        }

        public string Name { get; }

        /// <summary>0 → 1 по мере перезарядки, для полоски в UI.</summary>
        public float Progress => FastCooldown.GetProgress(attackKey);

        public bool TryAttack()
        {
            // Check = IsReady + Set одной операцией: удар прошёл — кулдаун сразу запущен.
            if (!FastCooldown.Check(attackKey, attackCooldown, out float remaining))
            {
                Debug.Log($"{Name}: ещё {remaining:F2} с");
                return false;
            }

            Debug.Log($"{Name}: удар");
            return true;
        }

        public void Dispose() => FastCooldown.RemoveAllFor(this);
    }
}
