using System.Collections;
using System.Collections.Generic;
using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using Unity.Barracuda;
using System.Linq;
using System.Text.RegularExpressions;

public class DimensionsBase
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Height { get; set; }
    public float Width { get; set; }
}

public class BoundingBoxDimensions : DimensionsBase { }

public class YoloBoundingBox
{
    public BoundingBoxDimensions Dimensions { get; set; }

    public string Label { get; set; }

    public float Confidence { get; set; }

    public Rect Rect { get; set; }
    public Color BoxColor { get; set; }
}

class YoloOutputParser
{
    class CellDimensions : DimensionsBase { }

    public const int ROW_COUNT = 13;
    public const int COL_COUNT = 13;
    public const int CHANNEL_COUNT = 125;
    public const int BOXES_PER_CELL = 5;
    public const int BOX_INFO_FEATURE_COUNT = 5;
    public const int CLASS_COUNT = 20;
    public const float CELL_WIDTH = 32;
    public const float CELL_HEIGHT = 32;

    private int channelStride = ROW_COUNT * COL_COUNT;

    private float[] anchors = new float[]
    {
            1.08F, 1.19F, 3.42F, 4.41F, 6.63F, 11.38F, 9.42F, 5.11F, 16.62F, 10.52F
    };

    private string[] labels = new string[]
    {
            "aeroplane", "bicycle", "bird", "boat", "bottle",
            "bus", "car", "cat", "chair", "cow",
            "diningtable", "dog", "horse", "motorbike", "person",
            "pottedplant", "sheep", "sofa", "train", "tvmonitor"
    };

    private static Color[] classColors = new Color[]
    {
            Color.cyan,
            Color.green,
            Color.magenta,
            Color.red,
            Color.yellow,
            Color.black,
            Color.blue,
            Color.cyan,
            Color.green,
            Color.magenta,
            Color.red,
            Color.yellow,
            Color.black,
            Color.blue,
            Color.cyan,
            Color.green,
            Color.magenta,
            Color.red,
            Color.yellow,
            Color.black,
            Color.blue,
    };

    private float Sigmoid(float value) {
        var k = (float)Math.Exp(value);
        return k / (1.0f + k);
    }

    private float[] Softmax(float[] values) {
        var maxVal = values.Max();
        var exp = values.Select(v => Math.Exp(v - maxVal));
        var sumExp = exp.Sum();

        return exp.Select(v => (float)(v / sumExp)).ToArray();
    }

    private int GetOffset(int x, int y, int channel) {
        // YOLO outputs a tensor that has a shape of 125x13x13, which 
        // WinML flattens into a 1D array.  To access a specific channel 
        // for a given (x,y) cell position, we need to calculate an offset
        // into the array
        return (channel * this.channelStride) + (y * COL_COUNT) + x;
    }

    private BoundingBoxDimensions ExtractBoundingBoxDimensions(Tensor modelOutput, int x, int y, int channel) {
        return new BoundingBoxDimensions {
            X = modelOutput[0, x, y, channel],
            Y = modelOutput[0, x, y, channel + 1],
            Width = modelOutput[0, x, y, channel + 2],
            Height = modelOutput[0, x, y, channel + 3]
        };
    }

    private float GetConfidence(Tensor modelOutput, int x, int y, int channel) {
        return Sigmoid(modelOutput[0, x, y, channel + 4]);
    }

    private CellDimensions MapBoundingBoxToCell(int x, int y, int box, BoundingBoxDimensions boxDimensions) {
        return new CellDimensions {
            X = ((float)x + Sigmoid(boxDimensions.X)) * CELL_WIDTH,
            Y = ((float)y + Sigmoid(boxDimensions.Y)) * CELL_HEIGHT,
            Width = (float)Math.Exp(boxDimensions.Width) * CELL_WIDTH * anchors[box * 2],
            Height = (float)Math.Exp(boxDimensions.Height) * CELL_HEIGHT * anchors[box * 2 + 1],
        };
    }

    public float[] ExtractClasses(Tensor modelOutput, int x, int y, int channel) {
        float[] predictedClasses = new float[CLASS_COUNT];
        int predictedClassOffset = channel + BOX_INFO_FEATURE_COUNT;
        for (int predictedClass = 0; predictedClass < CLASS_COUNT; predictedClass++) {
            predictedClasses[predictedClass] = modelOutput[0, x, y, predictedClass + predictedClassOffset];
        }
        return Softmax(predictedClasses);
    }

    private ValueTuple<int, float> GetTopResult(float[] predictedClasses) {
        return predictedClasses
            .Select((predictedClass, index) => (Index: index, Value: predictedClass))
            .OrderByDescending(result => result.Value)
            .First();
    }

    private float IntersectionOverUnion(Rect boundingBoxA, Rect boundingBoxB) {
        var areaA = boundingBoxA.width * boundingBoxA.height;

        if (areaA <= 0)
            return 0;

        var areaB = boundingBoxB.width * boundingBoxB.height;

        if (areaB <= 0)
            return 0;

        var minX = Math.Max(boundingBoxA.left, boundingBoxB.left);
        var minY = Math.Max(boundingBoxA.top, boundingBoxB.top);
        var maxX = Math.Min(boundingBoxA.right, boundingBoxB.right);
        var maxY = Math.Min(boundingBoxA.bottom, boundingBoxB.bottom);

        var intersectionArea = Math.Max(maxY - minY, 0) * Math.Max(maxX - minX, 0);

        return intersectionArea / (areaA + areaB - intersectionArea);
    }

    public IList<YoloBoundingBox> ParseOutputs(Tensor yoloModelOutputs, float threshold = .3F) {
        var boxes = new List<YoloBoundingBox>();

        {
            int row = 6;
            int column = 9;
            int channel = 75;
            float confidence = GetConfidence(yoloModelOutputs, row, column, channel);
            Debug.Log("6,9,75=" + confidence);
            Debug.Log("mo = " + yoloModelOutputs[0, 6, 9, channel + 4]);
        }

        for (int row = 0; row < ROW_COUNT; row++) {
            for (int column = 0; column < COL_COUNT; column++) {
                for (int box = 0; box < BOXES_PER_CELL; box++) {
                    var channel = (box * (CLASS_COUNT + BOX_INFO_FEATURE_COUNT));

                    BoundingBoxDimensions boundingBoxDimensions = ExtractBoundingBoxDimensions(yoloModelOutputs, row, column, channel);

                    float confidence = GetConfidence(yoloModelOutputs, row, column, channel);

                    CellDimensions mappedBoundingBox = MapBoundingBoxToCell(row, column, box, boundingBoxDimensions);

                    if (confidence < threshold)
                        continue;

                    float[] predictedClasses = ExtractClasses(yoloModelOutputs, row, column, channel);

                    var (topResultIndex, topResultScore) = GetTopResult(predictedClasses);
                    var topScore = topResultScore * confidence;

                    if (topScore < threshold)
                        continue;

                    boxes.Add(new YoloBoundingBox() {
                        Dimensions = new BoundingBoxDimensions {
                            X = (mappedBoundingBox.X - mappedBoundingBox.Width / 2),
                            Y = (mappedBoundingBox.Y - mappedBoundingBox.Height / 2),
                            Width = mappedBoundingBox.Width,
                            Height = mappedBoundingBox.Height,
                        },
                        Confidence = topScore,
                        Label = labels[topResultIndex],
                        BoxColor = classColors[topResultIndex]
                    });
                }
            }
        }
        return boxes;
    }

    public IList<YoloBoundingBox> FilterBoundingBoxes(IList<YoloBoundingBox> boxes, int limit, float threshold) {
        var activeCount = boxes.Count;
        var isActiveBoxes = new bool[boxes.Count];

        for (int i = 0; i < isActiveBoxes.Length; i++)
            isActiveBoxes[i] = true;

        var sortedBoxes = boxes.Select((b, i) => new { Box = b, Index = i })
                            .OrderByDescending(b => b.Box.Confidence)
                            .ToList();

        var results = new List<YoloBoundingBox>();

        for (int i = 0; i < boxes.Count; i++) {
            if (isActiveBoxes[i]) {
                var boxA = sortedBoxes[i].Box;
                results.Add(boxA);

                if (results.Count >= limit)
                    break;

                for (var j = i + 1; j < boxes.Count; j++) {
                    if (isActiveBoxes[j]) {
                        var boxB = sortedBoxes[j].Box;

                        if (IntersectionOverUnion(boxA.Rect, boxB.Rect) > threshold) {
                            isActiveBoxes[j] = false;
                            activeCount--;

                            if (activeCount <= 0)
                                break;
                        }
                    }
                }

                if (activeCount <= 0)
                    break;
            }
        }
        return results;
    }

}

public class TinyYolo : MonoBehaviour
{
    public NNModel modelAsset;
    private Model m_RuntimeModel;
    private IWorker m_worker;

    // Start is called before the first frame update
    void Start()
    {
        m_RuntimeModel = ModelLoader.Load(modelAsset);
        m_worker = WorkerFactory.CreateWorker(WorkerFactory.Type.Compute, m_RuntimeModel);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void doPredict(Texture2D texture) {
        // 推論
#if false
        var picture = texture.GetPixels32();
        float[] floatValues = new float[416 * 416 * 3];
        for(int i = 0; i < picture.Length; ++i) {
            var c = picture[i];
            var IMAGE_MEAN = 0;
            var IMAGE_STD = 1f;
            floatValues[i * 3 + 0] = c.r / 255f; //(c.r - IMAGE_MEAN) / IMAGE_STD;
            floatValues[i * 3 + 1] = c.g / 255f; //(c.g - IMAGE_MEAN) / IMAGE_STD;
            floatValues[i * 3 + 2] = c.b / 255f; //(c.b - IMAGE_MEAN) / IMAGE_STD;
        }
        Tensor input = new Tensor(1, 416, 416, 3, floatValues);
#endif
#if true
        Tensor input = new Tensor(texture);
#endif
        input.Print();

        // m_worker.Execute(input);
        // Tensor output = m_worker.PeekOutput();
        Tensor output = m_worker.ExecuteAndWaitForCompletion(input);
        output.Print();

        var yop = new YoloOutputParser();
        var output_bb = yop.ParseOutputs(output, 0.3f);

        foreach(var bb in output_bb) {
            Debug.Log("------");
            Debug.Log($"{bb.Label} : {bb.Confidence}");
        }
    }
}
