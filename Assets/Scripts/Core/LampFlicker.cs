using System.Collections;
using UnityEngine;

// 낡은 전등 느낌: 평소엔 아주 미세하게 웅웅거리듯 밝기가 떨리다가, 가끔(몇 초에 한 번) 살짝 "깜빡" 어두워졌다 돌아온다.
// 공포 느낌이 나지 않게, 꺼지지 않고 조금만(기본 75%까지) 어두워진다.
// 원리: 처음 밝기(Intensity)를 기억해 두고, 매 프레임 "처음 밝기 × (아주 작은 떨림) × (깜빡임)"으로 다시 정한다.
//       깜빡임은 무작위 간격으로 1~3번 짧게 내려갔다 올라온다. 전등마다 간격이 달라서 동시에 깜빡이지 않는다.
// 사용: 깜빡일 전등의 Light 오브젝트(Point Light 등)에 붙인다. 여러 개면 각각 붙인다.
// 주의: Light의 Mode가 Baked면 실행 중 밝기를 바꿔도 화면에 반영되지 않는다 → Mode를 Realtime으로 바꾸고,
//       바꾼 뒤엔 Lighting 창에서 Generate Lighting을 한 번 다시 누른다 (구운 빛에서 그 전등이 빠지므로).
[RequireComponent(typeof(Light))]
public class LampFlicker : MonoBehaviour
{
    [Header("평소 떨림 (웅웅거림)")]
    [Range(0f, 0.1f)] public float hum = 0.02f;          // 밝기가 떨리는 폭 (0.02 = ±2%)
    public float humSpeed = 6f;                          // 떨리는 빠르기

    [Header("가끔 깜빡임")]
    public float minGap = 4f;                            // 깜빡임 사이 최소 간격 (초)
    public float maxGap = 10f;                           // 깜빡임 사이 최대 간격 (초)
    [Range(0f, 1f)] public float dipTo = 0.75f;          // 깜빡일 때 처음 밝기의 몇 %까지 어두워질지 (0.75 = 75%)
    public int maxDips = 3;                              // 한 번에 깜빡이는 최대 횟수 (1~이 값 사이 무작위)

    [Header("전등 갓도 같이 (선택)")]
    public Renderer shade;                               // 빛나는 전등 갓·전구 모델 (비워도 됨)
    public Color shadeEmission = Color.white;            // 그 모델의 원래 빛 색 (Emission Color와 같게)

    Light lamp;
    float baseIntensity;
    float dip = 1f;                                      // 지금 깜빡임 배율 (1 = 정상)
    float seed;
    MaterialPropertyBlock block;
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    void Awake()
    {
        lamp = GetComponent<Light>();
        baseIntensity = lamp.intensity;
        seed = Random.value * 100f;                      // 전등마다 떨림 모양이 다르게
        block = new MaterialPropertyBlock();
    }

    void OnEnable() { StartCoroutine(FlickerLoop()); }

    void Update()
    {
        float h = 1f + (Mathf.PerlinNoise(seed, Time.time * humSpeed) * 2f - 1f) * hum;
        float k = h * dip;
        lamp.intensity = baseIntensity * k;

        if (shade != null)
        {
            shade.GetPropertyBlock(block);
            block.SetColor(EmissionId, shadeEmission * k);
            shade.SetPropertyBlock(block);
        }
    }

    IEnumerator FlickerLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(minGap, maxGap));
            int count = Random.Range(1, Mathf.Max(1, maxDips) + 1);
            for (int i = 0; i < count; i++)
            {
                float low = Random.Range(dipTo, Mathf.Lerp(dipTo, 1f, 0.5f));   // 매번 조금씩 다른 깊이
                yield return Fade(low, Random.Range(0.04f, 0.08f));             // 툭 어두워지고
                yield return Fade(1f, Random.Range(0.08f, 0.2f));               // 조금 천천히 돌아옴
                yield return new WaitForSeconds(Random.Range(0.03f, 0.12f));
            }
        }
    }

    IEnumerator Fade(float to, float time)
    {
        float from = dip;
        for (float t = 0f; t < 1f; t += Time.deltaTime / time)
        {
            dip = Mathf.Lerp(from, to, t);
            yield return null;
        }
        dip = to;
    }

    void OnDisable()
    {
        if (lamp != null) lamp.intensity = baseIntensity;
        dip = 1f;
    }
}