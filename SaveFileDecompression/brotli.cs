using System.Runtime.InteropServices;

public class brotli
{
    private const string libname = "libbrotli";
    public static IntPtr nativeBuffer = IntPtr.Zero;
    public static bool nativeBufferIsBeingUsed = false;
    public static int nativeOffset = 0;

    [DllImport("libbrotli")]
    internal static extern int brCompress(
      string inFile,
      string outFile,
      IntPtr proc,
      int quality,
      int lgwin,
      int lgblock,
      int mode);

    [DllImport("libbrotli")]
    internal static extern int brDecompresss(
      string inFile,
      string outFile,
      IntPtr proc,
      IntPtr FileBuffer,
      int fileBufferLength);

    [DllImport("libbrotli")]
    public static extern void brReleaseBuffer(IntPtr buffer);

    [DllImport("libbrotli")]
    public static extern IntPtr brCreate_Buffer(int size);

    [DllImport("libbrotli")]
    private static extern void brAddTo_Buffer(
      IntPtr destination,
      int offset,
      IntPtr buffer,
      int len);

    [DllImport("libbrotli")]
    internal static extern IntPtr brCompressBuffer(
      int bufferLength,
      IntPtr buffer,
      IntPtr encodedSize,
      IntPtr proc,
      int quality,
      int lgwin,
      int lgblock,
      int mode);

    [DllImport("libbrotli")]
    internal static extern int brGetDecodedSize(int bufferLength, IntPtr buffer);

    [DllImport("libbrotli")]
    internal static extern int brDecompressBuffer(
      int bufferLength,
      IntPtr buffer,
      int outLength,
      IntPtr outbuffer);

    public static int setFilePermissions(
      string filePath,
      string _user,
      string _group,
      string _other)
    {
        return -1;
    }

    internal static GCHandle gcA(object o) => GCHandle.Alloc(o, GCHandleType.Pinned);

    private static bool checkObject(
      object fileBuffer,
      string filePath,
      ref GCHandle fbuf,
      ref IntPtr fileBufferPointer,
      ref int fileBufferLength)
    {
        switch (fileBuffer)
        {
            case byte[] _:
                byte[] o = (byte[])fileBuffer;
                fbuf = brotli.gcA((object)o);
                fileBufferPointer = fbuf.AddrOfPinnedObject();
                fileBufferLength = o.Length;
                return true;
            case IntPtr num:
                fileBufferPointer = num;
                fileBufferLength = Convert.ToInt32(filePath);
                break;
        }
        return false;
    }

    public static int compressFile(
      string inFile,
      string outFile,
      ulong[] proc,
      int quality = 9,
      int lgwin = 19,
      int lgblock = 0,
      int mode = 0)
    {
        if (!File.Exists(inFile))
            return -5;
        if (quality < 0)
            quality = 1;
        if (quality > 11)
            quality = 11;
        if (lgwin < 10)
            lgwin = 10;
        if (lgwin > 24)
            lgwin = 24;
        if (proc == null)
            proc = new ulong[1];
        GCHandle gcHandle = GCHandle.Alloc((object)proc, GCHandleType.Pinned);
        int num = brotli.brCompress(inFile, outFile, gcHandle.AddrOfPinnedObject(), quality, lgwin, lgblock, mode);
        gcHandle.Free();
        return num;
    }

    public static int decompressFile(string inFile, string outFile, ulong[] proc, object fileBuffer = null)
    {
        if (fileBuffer == null && !File.Exists(inFile))
            return -5;
        if (proc == null)
            proc = new ulong[1];
        GCHandle gcHandle = GCHandle.Alloc((object)proc, GCHandleType.Pinned);
        int num = brotli.brDecompresss(inFile, outFile, gcHandle.AddrOfPinnedObject(), IntPtr.Zero, 0);
        gcHandle.Free();
        return num;
    }

    public static int getDecodedSize(byte[] inBuffer)
    {
        GCHandle gcHandle = GCHandle.Alloc((object)inBuffer, GCHandleType.Pinned);
        int decodedSize = brotli.brGetDecodedSize(inBuffer.Length, gcHandle.AddrOfPinnedObject());
        gcHandle.Free();
        return decodedSize;
    }

    public static bool compressBuffer(
      byte[] inBuffer,
      ref byte[] outBuffer,
      ulong[] proc,
      bool includeSize = false,
      int quality = 9,
      int lgwin = 19,
      int lgblock = 0,
      int mode = 0)
    {
        if (quality < 0)
            quality = 1;
        if (quality > 11)
            quality = 11;
        if (lgwin < 10)
            lgwin = 10;
        if (lgwin > 24)
            lgwin = 24;
        GCHandle gcHandle1 = GCHandle.Alloc((object)inBuffer, GCHandleType.Pinned);
        int num1 = 0;
        byte[] numArray1 = (byte[])null;
        int[] numArray2 = new int[1];
        GCHandle gcHandle2 = GCHandle.Alloc((object)numArray2, GCHandleType.Pinned);
        byte[] numArray3;
        if (includeSize)
        {
            numArray3 = new byte[4];
            num1 = 4;
            numArray1 = BitConverter.GetBytes(inBuffer.Length);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse<byte>(numArray1);
        }
        if (proc == null)
            proc = new ulong[1];
        GCHandle gcHandle3 = GCHandle.Alloc((object)proc, GCHandleType.Pinned);
        IntPtr num2 = brotli.brCompressBuffer(inBuffer.Length, gcHandle1.AddrOfPinnedObject(), gcHandle2.AddrOfPinnedObject(), gcHandle3.AddrOfPinnedObject(), quality, lgwin, lgblock, mode);
        gcHandle1.Free();
        gcHandle2.Free();
        gcHandle3.Free();
        int[] numArray4;
        if (num2 == IntPtr.Zero)
        {
            brotli.brReleaseBuffer(num2);
            numArray4 = (int[])null;
            numArray3 = (byte[])null;
            return false;
        }
        Array.Resize<byte>(ref outBuffer, numArray2[0] + num1);
        if (includeSize)
        {
            for (int index = 0; index < 4; ++index)
                outBuffer[index + numArray2[0]] = numArray1[index];
        }
        Marshal.Copy(num2, outBuffer, 0, numArray2[0]);
        brotli.brReleaseBuffer(num2);
        numArray4 = (int[])null;
        numArray3 = (byte[])null;
        return true;
    }

    public static byte[] compressBuffer(
      byte[] inBuffer,
      int[] proc,
      bool includeSize = false,
      int quality = 9,
      int lgwin = 19,
      int lgblock = 0,
      int mode = 0)
    {
        if (quality < 0)
            quality = 1;
        if (quality > 11)
            quality = 11;
        if (lgwin < 10)
            lgwin = 10;
        if (lgwin > 24)
            lgwin = 24;
        GCHandle gcHandle1 = GCHandle.Alloc((object)inBuffer, GCHandleType.Pinned);
        int num1 = 0;
        byte[] numArray1 = (byte[])null;
        int[] numArray2 = new int[1];
        GCHandle gcHandle2 = GCHandle.Alloc((object)numArray2, GCHandleType.Pinned);
        byte[] numArray3;
        if (includeSize)
        {
            numArray3 = new byte[4];
            num1 = 4;
            numArray1 = BitConverter.GetBytes(inBuffer.Length);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse<byte>(numArray1);
        }
        if (proc == null)
            proc = new int[1];
        GCHandle gcHandle3 = GCHandle.Alloc((object)proc, GCHandleType.Pinned);
        IntPtr num2 = brotli.brCompressBuffer(inBuffer.Length, gcHandle1.AddrOfPinnedObject(), gcHandle2.AddrOfPinnedObject(), gcHandle3.AddrOfPinnedObject(), quality, lgwin, lgblock, mode);
        gcHandle1.Free();
        gcHandle2.Free();
        gcHandle3.Free();
        int[] numArray4;
        if (num2 == IntPtr.Zero)
        {
            brotli.brReleaseBuffer(num2);
            numArray4 = (int[])null;
            numArray3 = (byte[])null;
            return (byte[])null;
        }
        byte[] destination = new byte[numArray2[0] + num1];
        if (includeSize)
        {
            for (int index = 0; index < 4; ++index)
                destination[index + numArray2[0]] = numArray1[index];
        }
        Marshal.Copy(num2, destination, 0, numArray2[0]);
        brotli.brReleaseBuffer(num2);
        numArray4 = (int[])null;
        numArray3 = (byte[])null;
        return destination;
    }

    public static int compressBuffer(
      byte[] inBuffer,
      byte[] outBuffer,
      int[] proc,
      bool includeSize = false,
      int quality = 9,
      int lgwin = 19,
      int lgblock = 0,
      int mode = 0)
    {
        if (quality < 0)
            quality = 1;
        if (quality > 11)
            quality = 11;
        if (lgwin < 10)
            lgwin = 10;
        if (lgwin > 24)
            lgwin = 24;
        GCHandle gcHandle1 = GCHandle.Alloc((object)inBuffer, GCHandleType.Pinned);
        int num1 = 0;
        byte[] numArray1 = (byte[])null;
        int[] numArray2 = new int[1];
        GCHandle gcHandle2 = GCHandle.Alloc((object)numArray2, GCHandleType.Pinned);
        byte[] numArray3;
        if (includeSize)
        {
            numArray3 = new byte[4];
            num1 = 4;
            numArray1 = BitConverter.GetBytes(inBuffer.Length);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse<byte>(numArray1);
        }
        if (proc == null)
            proc = new int[1];
        GCHandle gcHandle3 = GCHandle.Alloc((object)proc, GCHandleType.Pinned);
        IntPtr num2 = brotli.brCompressBuffer(inBuffer.Length, gcHandle1.AddrOfPinnedObject(), gcHandle2.AddrOfPinnedObject(), gcHandle3.AddrOfPinnedObject(), quality, lgwin, lgblock, mode);
        gcHandle1.Free();
        gcHandle2.Free();
        gcHandle3.Free();
        int num3 = numArray2[0];
        int[] numArray4;
        if (num2 == IntPtr.Zero || outBuffer.Length < numArray2[0] + num1)
        {
            brotli.brReleaseBuffer(num2);
            numArray4 = (int[])null;
            numArray3 = (byte[])null;
            return 0;
        }
        Marshal.Copy(num2, outBuffer, 0, numArray2[0]);
        if (includeSize)
        {
            for (int index = 0; index < 4; ++index)
                outBuffer[index + numArray2[0]] = numArray1[index];
        }
        brotli.brReleaseBuffer(num2);
        numArray4 = (int[])null;
        numArray3 = (byte[])null;
        return num3 + num1;
    }

    public static bool decompressBuffer(
      byte[] inBuffer,
      ref byte[] outBuffer,
      bool useFooter = false,
      int unCompressedSize = 0)
    {
        GCHandle gcHandle1 = GCHandle.Alloc((object)inBuffer, GCHandleType.Pinned);
        int length = inBuffer.Length;
        int num1;
        if (unCompressedSize == 0)
        {
            if (useFooter)
            {
                int startIndex = length - 4;
                num1 = BitConverter.ToInt32(inBuffer, startIndex);
            }
            else
                num1 = brotli.getDecodedSize(inBuffer);
        }
        else
            num1 = unCompressedSize;
        Array.Resize<byte>(ref outBuffer, num1);
        GCHandle gcHandle2 = GCHandle.Alloc((object)outBuffer, GCHandleType.Pinned);
        int num2 = brotli.brDecompressBuffer(inBuffer.Length, gcHandle1.AddrOfPinnedObject(), num1, gcHandle2.AddrOfPinnedObject());
        gcHandle1.Free();
        gcHandle2.Free();
        return num2 == 1;
    }

    public static byte[] decompressBuffer(byte[] inBuffer, bool useFooter = false, int unCompressedSize = 0)
    {
        GCHandle gcHandle1 = GCHandle.Alloc((object)inBuffer, GCHandleType.Pinned);
        int length = inBuffer.Length;
        int outLength;
        if (unCompressedSize == 0)
        {
            if (useFooter)
            {
                int startIndex = length - 4;
                outLength = BitConverter.ToInt32(inBuffer, startIndex);
            }
            else
                outLength = brotli.getDecodedSize(inBuffer);
        }
        else
            outLength = unCompressedSize;
        byte[] numArray = new byte[outLength];
        GCHandle gcHandle2 = GCHandle.Alloc((object)numArray, GCHandleType.Pinned);
        int num = brotli.brDecompressBuffer(inBuffer.Length, gcHandle1.AddrOfPinnedObject(), outLength, gcHandle2.AddrOfPinnedObject());
        gcHandle1.Free();
        gcHandle2.Free();
        return num == 1 ? numArray : (byte[])null;
    }

    public static int decompressBuffer(
      byte[] inBuffer,
      byte[] outBuffer,
      bool useFooter = false,
      int unCompressedSize = 0)
    {
        GCHandle gcHandle1 = GCHandle.Alloc((object)inBuffer, GCHandleType.Pinned);
        int length = inBuffer.Length;
        int outLength;
        if (unCompressedSize == 0)
        {
            if (useFooter)
            {
                int startIndex = length - 4;
                outLength = BitConverter.ToInt32(inBuffer, startIndex);
            }
            else
                outLength = brotli.getDecodedSize(inBuffer);
        }
        else
            outLength = unCompressedSize;
        GCHandle gcHandle2 = GCHandle.Alloc((object)outBuffer, GCHandleType.Pinned);
        int num = brotli.brDecompressBuffer(inBuffer.Length, gcHandle1.AddrOfPinnedObject(), outLength, gcHandle2.AddrOfPinnedObject());
        gcHandle1.Free();
        gcHandle2.Free();
        return num == 1 ? outLength : 0;
    }
}
