using UnityEngine;

namespace TeamRoulette.Room3.BallPlay
{
    public sealed class BallContact : MonoBehaviour
    {
        public BallGame owner;
        void OnCollisionEnter(Collision collision)
        {
            if (owner) owner.ReportBallContact(collision);
        }
    }
}
