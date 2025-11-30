using Microsoft.ML.OnnxRuntimeGenAI;
var modelPath = @"C:\Users\songs\.cache\huggingface\hub\models--llmware--gemma-2b-it-onnx\snapshots\5ad21eec704fe31921655f5ddf145e2fc49ed6b1";
Console.WriteLine($"Model path: {modelPath}");
Console.WriteLine($"Exists: {Directory.Exists(modelPath)}");
foreach (var f in Directory.GetFiles(modelPath)) {
    Console.WriteLine($"  {Path.GetFileName(f)}: {new FileInfo(f).Length} bytes");
}
try {
    Console.WriteLine("Loading model...");
    var model = new Model(modelPath);
    Console.WriteLine("Model loaded!");
    var tokenizer = new Tokenizer(model);
    Console.WriteLine("Tokenizer loaded!");
} catch (Exception ex) {
    Console.WriteLine($"ERROR: {ex.GetType().FullName}");
    Console.WriteLine($"Message: {ex.Message}");
    if (ex.InnerException != null) {
        Console.WriteLine($"Inner: {ex.InnerException.Message}");
    }
}
