using System;

namespace ARZone
{
    // 큐브 소유권 인증 — POST /api/v1/cubes/verification
    [Serializable]
    public class CubeVerifyReq
    {
        public int cubeId;
        public string ownershipCode;
    }

    [Serializable]
    public class CubeVerifyRes
    {
        public int cubeOwnershipId;
    }
}
