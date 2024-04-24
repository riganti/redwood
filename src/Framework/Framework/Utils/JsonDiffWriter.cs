// using System.Linq;
// using System;
// using System.Text.Json;
// using System.Diagnostics;
// using System.Buffers;
// using DotVVM.Framework.ViewModel.Serialization;
// using System.Runtime.CompilerServices;

// namespace DotVVM.Framework.Utils
// {
//     ref struct JsonDiffWriter
//     {
//         static void AssertToken(ref Utf8JsonReader reader, JsonTokenType expected)
//         {
//             if (reader.TokenType != expected)
//                 throw new JsonException($"Expected {expected} but got {reader.TokenType}.");
//         }
//         static void CopyValue(ref Utf8JsonReader reader, Utf8JsonWriter writer)
//         {
//             Debug.Assert(reader.TokenType != JsonTokenType.PropertyName);

//             if (reader.TokenType is not JsonTokenType.StartArray and not JsonTokenType.StartObject)
//             {
//                 if (reader.HasValueSequence)
//                     writer.WriteRawValue(reader.ValueSequence);
//                 else
//                     writer.WriteRawValue(reader.ValueSpan);

//                 return;
//             }

//             var depth = reader.CurrentDepth;
//             while (reader.CurrentDepth >= depth)
//             {
//                 CopyToken(ref reader, writer);
//                 reader.Read();
//             }
//         }

//         static void CopyToken(ref Utf8JsonReader reader, Utf8JsonWriter writer)
//         {
//             switch (reader.TokenType)
//             {
//                 case JsonTokenType.False:
//                 case JsonTokenType.True:
//                 case JsonTokenType.Null:
//                 case JsonTokenType.String:
//                 case JsonTokenType.Number: {
//                     if (reader.HasValueSequence)
//                         writer.WriteRawValue(reader.ValueSequence);
//                     else
//                         writer.WriteRawValue(reader.ValueSpan);
//                     break;
//                 }
//                 case JsonTokenType.PropertyName: {
//                     var length = reader.HasValueSequence ? reader.ValueSequence.Length : reader.ValueSpan.Length;
//                     Span<byte> buffer = length <= 1024 ? stackalloc byte[(int)length] : new byte[length];
//                     var realLength = reader.CopyString(buffer);
//                     writer.WritePropertyName(buffer.Slice(0, realLength));
//                     break;
//                 }
//                 case JsonTokenType.StartArray: {
//                     writer.WriteStartArray();
//                     break;
//                 }
//                 case JsonTokenType.EndArray: {
//                     writer.WriteEndArray();
//                     break;
//                 }
//                 case JsonTokenType.StartObject: {
//                     writer.WriteStartObject();
//                     break;
//                 }
//                 case JsonTokenType.EndObject: {
//                     writer.WriteEndObject();
//                     break;
//                 }
//                 default: {
//                     throw new JsonException($"Unexpected token {reader.TokenType}.");
//                 }
//             }
//         }
        
//         public delegate bool? IncludePropertyDelegate(string typeId, ReadOnlySpan<byte> propertyName);
//         private readonly IncludePropertyDelegate? includePropertyOverride;

//         private Utf8JsonReader reader;
//         private ReadOnlySequence<byte> remainingPreviousInput;

//         private byte[]? nameBufferRented;
//         private Span<byte> nameBuffer;
//         private int nameBufferPosition;
//         private RefList<int> lazyWriteStack;
//         private readonly Utf8JsonWriter writer;
//         private RefList<JsonElement> sourceElementStack;
//         private RefList<ClientTypeId> objectTypeIds;
//         private JsonDiffState state;

//         private JsonDiffWriter(
//             IncludePropertyDelegate? includePropertyOverride,
//             Span<byte> nameBuffer,
//             Utf8JsonWriter writer,
//             JsonElement sourceElement
//         )
//         {
//             this.includePropertyOverride = includePropertyOverride;
//             this.nameBuffer = nameBuffer;
//             this.writer = writer;
//             this.sourceElementStack = new RefList<JsonElement>(default);
//             this.sourceElementStack.Enlarge(32);
//             this.sourceElementStack.Add(sourceElement);
//             this.objectTypeIds = new RefList<ClientTypeId>(default);
//             this.objectTypeIds.Enlarge(32);
//             this.objectTypeIds.Add(default);
//         }

//         public (int bytesRead, bool isDone) ContinueDiff(Span<byte> target, bool isFinalBlock)
//         {
//             var startPosition = reader.BytesConsumed;
//             var position = startPosition;
//             if (startPosition == 0)
//             {
//                 reader = new Utf8JsonReader(target, isFinalBlock, default);
//             }
//             else
//             {
//                 reader = new Utf8JsonReader(target, isFinalBlock, reader.CurrentState);
//             }
//             if (!reader.Read())
//                 goto EndOfInput;

//             while (true)
//             {
//                 if (state == JsonDiffState.Copying)
//                 {
//                     CopyToken(ref reader, writer);
//                     reader.Read();
//                 }
//                 switch (reader.TokenType)
//                 {
//                     case JsonTokenType.StartObject:
//                         if (sourceElementStack.Last.ValueKind != JsonValueKind.Object)
//                         {
//                             CopyValue(ref reader, writer);
//                             state = JsonDiffState.Copying;
//                         }
//                         else
//                         {
//                             state = JsonDiffState.DiffObject;
//                         }
//                         break;
//                     case JsonTokenType.EndObject:
//                         break;
//                     case JsonTokenType.StartArray:
//                         break;
//                     case JsonTokenType.EndArray:
//                         break;
//                     case JsonTokenType.PropertyName: {
//                         var name = ReadName(ref reader);
//                         if (!reader.Read())
//                         {
//                             state = JsonDiffState.DiffValue;
//                             goto EndOfInput;
//                         }
//                         break;
//                     }
//                     case JsonTokenType.Comment:
//                         break;
//                     case JsonTokenType.String:
//                         break;
//                     case JsonTokenType.Number:
//                         break;
//                     case JsonTokenType.True:
//                         break;
//                     case JsonTokenType.False:
//                         break;
//                     case JsonTokenType.Null:
//                         break;
//                 }
//             }

//             return ((int)(reader.BytesConsumed - startPosition), true);


//             EndOfInput:
//             return ((int)(reader.BytesConsumed - startPosition), false);
//         }

//         Span<byte> ReadName(ref Utf8JsonReader reader)
//         {
//             var length = reader.CopyString(nameBuffer.Slice(nameBufferPosition));
//             if (length == 0)
//                 throw new JsonException("Empty property name.");
//             if (length < nameBuffer.Length)
//             {
//                 return nameBuffer.Slice(nameBufferPosition, length);
//             }
//             var newBuffer = ArrayPool<byte>.Shared.Rent(length);
//             nameBuffer.CopyTo(newBuffer);
//             if (nameBufferRented is {})
//                 ArrayPool<byte>.Shared.Return(nameBufferRented);
//             nameBuffer = newBuffer;
//             nameBufferRented = newBuffer;
//             return ReadName(ref reader);
//         }

//         void WriteoutLazyStack()
//         {
//             // foreach (var x in lazyWriteStack.AsSpan())
//             // {
//             //     if (x <= 0)
//             //     {
//             //         writer.Write TODO
//             //     }
//             // }
//         }

//         void AddPropertyToStack(ReadOnlySpan<byte> propertyName)
//         {
//             nameBufferPosition = nameBufferPosition + propertyName.Length;
//             lazyWriteStack.Add(nameBufferPosition);
//         }

//         void AddArrayToStack()
//         {
//             lazyWriteStack.Add(0);
//         }

//         void DiffObject(in JsonElement source, ref Utf8JsonReader target)
//         {
//             AssertToken(ref target, JsonTokenType.StartObject);
//             if (source.ValueKind != JsonValueKind.Object)
//             {
//                 CopyValue(ref target, writer);
//                 return;
//             }

//             string? typeId = null;
//             target.Read();
//             // var typeId = target.TryGetValue("$type", out var t) ? t.Value<string>() : null;
            
//             while (target.TokenType != JsonTokenType.EndObject)
//             {
//                 AssertToken(ref target, JsonTokenType.PropertyName);
//                 var propertyName = ReadName(ref target);

//                 if (propertyName[0] == '$')
//                 {
//                     if (propertyName.SequenceEqual("$type"u8))
//                     {
//                         typeId = target.GetString();
//                     }
//                 }
//                 else
//                 {
//                     if (typeId is {} && includePropertyOverride is {})
//                     {
//                         var include = includePropertyOverride(typeId, propertyName);
//                         if (include == true)
//                         {
//                             writer.WritePropertyName(propertyName);
//                             CopyValue(ref target, writer);
//                             continue;
//                         }
//                         else if (include == false)
//                         {
//                             continue;
//                         }
//                     }
//                 }

//                 if (!source.TryGetProperty(propertyName, out var sourceValue))
//                 {
//                     writer.WritePropertyName(propertyName);
//                     CopyValue(ref target, writer);
//                     continue;
//                 }

//                 if (sourceValue.ValueKind == JsonValueKind.Object && target.TokenType == JsonTokenType.StartObject)
//                 {
//                     writer.WritePropertyName(propertyName);
//                     DiffObject(sourceValue, ref target);
//                 }
//                 else if (sourceValue.ValueKind == JsonValueKind.Array && target.TokenType == JsonTokenType.StartArray)
//                 {
//                     writer.WritePropertyName(propertyName);
//                     DiffArray(sourceValue, ref target);
//                 }
//                 else if 
//             }
//             foreach (var item in target)
//             {
//                 if (sourceItem.Type == JTokenType.Array)
//                 {
//                     var sourceArr = (JArray)sourceItem;
//                     var subchanged = false;
//                     var arrDiff = Diff(sourceArr, (JArray)item.Value, out subchanged, nullOnRemoved);
//                     if (subchanged)
//                     {
//                         diff[item.Key] = arrDiff;
//                     }
//                 }
//                 else if (!JToken.DeepEquals(sourceItem, item.Value))
//                 {
//                     diff[item.Key] = item.Value;
//                 }
//             }

//             if (nullOnRemoved)
//             {
//                 foreach (var item in source)
//                 {
//                     if (target[item.Key] == null) diff[item.Key] = JValue.CreateNull();
//                 }
//             }
//             return diff;
//         }

//         enum JsonDiffState
//         {
//             DiffValue,
//             DiffObject,
//             Copying
//         }


//         ref struct RefList<T>
//         {
//             Span<T> buffer;
//             T[]? rented;
//             int count;

//             public RefList(Span<T> initialCapacity)
//             {
//                 this.buffer = initialCapacity;
//                 this.rented = null;
//                 this.count = 0;
//             }

//             public void Enlarge(int newCapacity)
//             {
//                 if (newCapacity <= buffer.Length)
//                     return;

//                 var newBuffer = ArrayPool<T>.Shared.Rent(Math.Max(newCapacity, buffer.Length * 2));
//                 buffer.CopyTo(newBuffer);
//                 if (rented is {})
//                     ArrayPool<T>.Shared.Return(rented);
//                 rented = newBuffer;
//                 buffer = newBuffer;
//             }

//             public void Add(T item)
//             {
//                 if (count == buffer.Length)
//                     Enlarge(buffer.Length + 8);
//                 buffer[count++] = item;
//             }

//             public void Pop()
//             {
//                 count--;
//                 if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
//                     buffer[count] = default!;
//             }

//             public void Clear()
//             {
//                 count = 0;

//                 if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
//                     buffer.Fill(default!);
//             }

//             public T? LastOrDefault() => count > 0 ? buffer[count - 1] : default;

//             public ref T this[int index] => ref buffer[index];
//             public ref T Last => ref buffer[count - 1];

//             public Span<T> AsSpan() => buffer.Slice(0, count);
//         }
//     }
// }
