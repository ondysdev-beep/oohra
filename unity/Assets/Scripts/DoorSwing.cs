using Oohra.Protocol;
using UnityEngine;

namespace Oohra.Client
{
    /// <summary>
    /// Dveře se samy otevřou, když jimi někdo prochází (směrem od něj), a pak se zavřou a chvíli dokývají.
    /// Je to jen vzhled: pohyb dveře neblokují, to rozhoduje server.
    /// Skript sedí na pantu dveří (otáčí se kolem svislé osy).
    /// </summary>
    public class DoorSwing : MonoBehaviour
    {
        // Jak blízko u dveří musí někdo být, aby se otevřely (v metrech).
        private const float OpenDistance = 0.8f;
        private const float OpenAngle = 85f;

        // Pružina: tuhost a tlumení. Málo tlumení = dveře se po zavření chvíli kývají.
        private const float Stiffness = 70f;
        private const float Damping = 3.5f;

        private GameConnection _connection;
        private Vector2 _center;
        private Vector2 _normal;
        private float _baseYaw;
        private float _velocity;

        /// <summary>Jak moc jsou dveře otevřené (stupně, 0 = zavřené, znaménko = na kterou stranu).</summary>
        public float Angle { get; private set; }

        /// <summary>(x, y) je střed otvoru na mapě, angle úhel zdi v radiánech.</summary>
        public void Init(GameConnection connection, double x, double y, float angle)
        {
            _connection = connection;
            _center = new Vector2((float)x, (float)y);
            _normal = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle));
            _baseYaw = transform.localEulerAngles.y;
        }

        /// <summary>
        /// Jeden krok pružiny k cílovému úhlu. Otevírání je rychlé, zavírání se překmitne a dokývá.
        /// Veřejné kvůli testům.
        /// </summary>
        public static void Step(ref float angle, ref float velocity, float target, float deltaTime)
        {
            var acceleration = Stiffness * (target - angle) - Damping * velocity;
            velocity += acceleration * deltaTime;
            angle += velocity * deltaTime;
        }

        private void Update()
        {
            if (_connection == null)
            {
                return;
            }

            var target = 0f;
            var snapshot = _connection.Snapshot;
            if (snapshot != null)
            {
                foreach (var player in snapshot.Players)
                {
                    var offset = new Vector2(player.X, player.Y) - _center;
                    if (offset.magnitude < OpenDistance)
                    {
                        // Dveře se otevřou na stranu od hráče. Kladný úhel otáčí křídlo k normále zdi.
                        target = Vector2.Dot(offset, _normal) > 0 ? -OpenAngle : OpenAngle;
                        break;
                    }
                }
            }

            // Krok rozdělený na menší kousky, ať je pružina stabilní i při trhaném snímkování.
            var dt = Mathf.Min(Time.deltaTime, 0.05f) / 4;
            var angle = Angle;
            for (var i = 0; i < 4; i++)
            {
                Step(ref angle, ref _velocity, target, dt);
            }

            Angle = Mathf.Clamp(angle, -100f, 100f);
            transform.localRotation = Quaternion.Euler(0f, _baseYaw + Angle, 0f);
        }
    }
}
