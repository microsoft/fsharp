module Neg131

module SmartHashUtils =
    let ByteToUInt (array:byte[]) offset length endian =

        let temp:uint32[] = Array.create (length / 4) (uint32 0)
        let ff = uint32 0xff

        match endian with
            | 0 -> 
                let funn i n =
                    (uint32 array.[offset + i*4] &&& ff) |||
                    ((uint32 array.[offset + i*4+1] &&& ff) <<< 8) |||
                    ((uint32 array.[offset + i*4+2] &&& ff) <<< 16) |||
                    ((uint32 array.[offset + i*4+3] &&& ff) <<< 24)
                Array.mapi funn temp
            | _ -> 
                let funn i n =
                    ((uint32 array.[offset + i*4] &&& ff) <<< 24) |||
                    ((uint32 array.[offset + i*4+1] &&& ff) <<< 16) |||
                    ((uint32 array.[offset + i*4+2] &&& ff) <<< 8) |||
                    (uint32 array.[offset + i*4+3] &&& ff)
                Array.mapi funn temp


    let UIntToByte (array:uint32[]) offset length endian =
        let temp:byte[] = Array.create (length * 4) (byte 0)
        
        match endian with
            | 0 -> 
                let funn i n = byte (array.[offset + i/4] >>> (i%4 * 8))  
                Array.mapi funn temp  
            | _ -> 
                let funn i n = byte (array.[offset + i/4] >>> ((3 - i%4) * 8))  
                Array.mapi funn temp  
                
                
    let ULongToByte (array:uint64[]) offset length endian =
        let temp:byte[] = Array.create (length * 8) (byte 0)
        
        match endian with
            | 0 -> 
                let funn i n = byte (array.[offset + i/8] >>> (i%8 * 8))  
                Array.mapi funn temp  
            | _ -> 
                let funn i n = byte (array.[offset + i/8] >>> ((7 - i%8) * 8))  
                Array.mapi funn temp  


    let LS (x:uint32) n = uint32 ((x <<< n) ||| (x >>> (32 - n))) 
    let RS (x:uint32) n = uint32 ((x >>> n) ||| (x <<< (32 - n)))

module SmartHashBlock =
    open System.Security.Cryptography

    [<AbstractClass>]
    type BlockHashAlgorithm() =
        inherit HashAlgorithm()
        ///The size in bytes of an individual block.
        let mutable blockSize = 1
        ///The length of bytes, that have been processed.
        ///This number includes the number of bytes currently waiting in the buffer.
        let mutable count:uint64 = uint64 0
        ///Buffer for storing leftover bytes that are waiting to be processed.
        let mutable buffer = Array.zeroCreate blockSize
        ///The number of bytes currently in the Buffer waiting to be processed.
        let mutable bufferCount = 0

        member b.BlockSize with get() = blockSize and set(v) = blockSize <- v
        member b.BufferCount with get() = bufferCount and set(v) = bufferCount <- v
        member b.Count with get() = count and set(v) = count <- v
        
        default x.Initialize() = 
            count <- uint64 0
            bufferCount <- 0
            buffer <- Array.zeroCreate blockSize
                
        default x.HashCore(array, ibStart, cbSize) =        
            //let engineUpdate input offset' length' =
            let mutable offset = ibStart
            let mutable length = cbSize
            count <- count + (uint64 length)

            if ((bufferCount > 0) && ((bufferCount + length) >= blockSize)) then
                let off = blockSize - bufferCount
                Array.blit array offset buffer bufferCount off
                offset <- offset + off
                length <- length - off
                bufferCount <- 0
                x.BlockTransform (buffer, 0)
            
            let numBlocks = length / blockSize
            for i in 0..(numBlocks-1) do
                x.BlockTransform (array, (offset + i * blockSize))
            
            let bytesLeft = length % blockSize
            
            if (bytesLeft <> 0) then
                Array.blit array (offset + (length - bytesLeft)) buffer bufferCount bytesLeft
                bufferCount <- bufferCount + bytesLeft
        
        abstract BlockTransform: (byte[] * int) -> unit
        
        member x.CreatePadding(minSize, append) = 
            let mutable paddingSize = x.BlockSize - ((int x.Count) % x.BlockSize)
            
            if (paddingSize < minSize) then paddingSize <- paddingSize + x.BlockSize    

            let Padding = Array.create paddingSize (byte 0)
            Padding.[0] <- append
            Padding

module SmartHashMD5 = 
    open SmartHashUtils
    open SmartHashBlock


    type MD5() as this =
        inherit BlockHashAlgorithm()
        ///The size in bytes of an individual block.
        let mutable state:int32[] = null
        
        do this.BlockSize <- 64
        do this.HashSizeValue <- 128
        do state <- Array.zeroCreate 4
        do this.Initialize()
        
        override x.Initialize() =
            base.Initialize()
            state.[0] <- 0x67452301
            state.[1] <- 0xEFCDAB89
            state.[2] <- 0x98BADCFE
            state.[3] <- 0x10325476

        member x.BlockTransform(data, iOffset) = 
            let mutable A = state.[0]
            let mutable B = state.[3]
            let mutable C = state.[2]
            let mutable D = state.[1]

            let X = ByteToUInt data iOffset 64 0

            A <- D +  LS (P1 D C B + A + X.[0] + uint32 0xD76AA478) 7    
            B <- A + LS(P1 A D C + B + X.[1] + uint32 0xE8C7B756) 12
            C <- B + LS(P1 B A D + C + X.[2] + uint32 0x242070DB) 17
            D <- C + LS(P1 C B A + D + X.[3] + uint32 0xC1BDCEEE) 22
            A <- D + LS(P1 D C B + A + X.[4] + uint32 0xF57C0FAF) 7
            B <- A + LS(P1 A D C + B + X.[5] + uint32 0x4787C62A) 12
            C <- B + LS(P1 B A D + C + X.[6] + uint32 0xA8304613) 17
            D <- C + LS(P1 C B A + D + X.[7] + uint32 0xFD469501) 22
            A <- D + LS(P1 D C B + A + X.[8] + uint32 0x698098D8) 7
            B <- A + LS(P1 A D C + B + X.[9] + uint32 0x8B44F7AF) 12
            C <- B + LS(P1 B A D + C + X.[10] + uint32 0xFFFF5BB1) 17
            D <- C + LS(P1 C B A + D + X.[11] + uint32 0x895CD7BE) 22
            A <- D + LS(P1 D C B + A + X.[12] + uint32 0x6B901122) 7
            B <- A + LS(P1 A D C + B + X.[13] + uint32 0xFD987193) 12
            C <- B + LS(P1 B A D + C + X.[14] + uint32 0xA679438E) 17
            D <- C + LS(P1 C B A + D + X.[15] + uint32 0x49B40821) 22

            A <- D + LS(P2 D C B + A + X.[1] + uint32 0xF61E2562) 5
            B <- A + LS(P2 A D C + B + X.[6] + uint32 0xC040B340) 9
            C <- B + LS(P2 B A D + C + X.[11] + uint32 0x265E5A51) 14
            D <- C + LS(P2 C B A + D + X.[0] + uint32 0xE9B6C7AA) 20
            A <- D + LS(P2 D C B + A + X.[5] + uint32 0xD62F105D) 5
            B <- A + LS(P2 A D C + B + X.[10] + uint32 0x02441453) 9
            C <- B + LS(P2 B A D + C + X.[15] + uint32 0xD8A1E681) 14
            D <- C + LS(P2 C B A + D + X.[4] + uint32 0xE7D3FBC8) 20
            A <- D + LS(P2 D C B + A + X.[9] + uint32 0x21E1CDE6) 5
            B <- A + LS(P2 A D C + B + X.[14] + uint32 0xC33707D6) 9
            C <- B + LS(P2 B A D + C + X.[3] + uint32 0xF4D50D87) 14
            D <- C + LS(P2 C B A + D + X.[8] + uint32 0x455A14ED) 20
            A <- D + LS(P2 D C B + A + X.[13] + uint32 0xA9E3E905) 5
            B <- A + LS(P2 A D C + B + X.[2] + uint32 0xFCEFA3F8) 9
            C <- B + LS(P2 B A D + C + X.[7] + uint32 0x676F02D9) 14
            D <- C + LS(P2 C B A + D + X.[12] + uint32 0x8D2A4C8A) 20

            A <- D + LS(P3 D C B + A + X.[5] + uint32 0xFFFA3942) 4
            B <- A + LS(P3 A D C + B + X.[8] + uint32 0x8771F681) 11
            C <- B + LS(P3 B A D + C + X.[11] + uint32 0x6D9D6122) 16
            D <- C + LS(P3 C B A + D + X.[14] + uint32 0xFDE5380C) 23
            A <- D + LS(P3 D C B + A + X.[1] + uint32 0xA4BEEA44) 4
            B <- A + LS(P3 A D C + B + X.[4] + uint32 0x4BDECFA9) 11
            C <- B + LS(P3 B A D + C + X.[7] + uint32 0xF6BB4B60) 16
            D <- C + LS(P3 C B A + D + X.[10] + uint32 0xBEBFBC70) 23
            A <- D + LS(P3 D C B + A + X.[13] + uint32 0x289B7EC6) 4
            B <- A + LS(P3 A D C + B + X.[0] + uint32 0xEAA127FA) 11
            C <- B + LS(P3 B A D + C + X.[3] + uint32 0xD4EF3085) 16
            D <- C + LS(P3 C B A + D + X.[6] + uint32 0x04881D05) 23
            A <- D + LS(P3 D C B + A + X.[9] + uint32 0xD9D4D039) 4
            B <- A + LS(P3 A D C + B + X.[12] + uint32 0xE6DB99E5) 11
            C <- B + LS(P3 B A D + C + X.[15] + uint32 0x1FA27CF8) 16
            D <- C + LS(P3 C B A + D + X.[2] + uint32 0xC4AC5665) 23

            A <- D + LS(P4 D C B + A + X.[0] + uint32 0xF4292244) 6
            B <- A + LS(P4 A D C + B + X.[7] + uint32 0x432AFF97) 10
            C <- B + LS(P4 B A D + C + X.[14] + uint32 0xAB9423A7) 15
            D <- C + LS(P4 C B A + D + X.[5] + uint32 0xFC93A039) 21
            A <- D + LS(P4 D C B + A + X.[12] + uint32 0x655B59C3) 6
            B <- A + LS(P4 A D C + B + X.[3] + uint32 0x8F0CCC92) 10
            C <- B + LS(P4 B A D + C + X.[10] + uint32 0xFFEFF47D) 15
            D <- C + LS(P4 C B A + D + X.[1] + uint32 0x85845DD1) 21
            A <- D + LS(P4 D C B + A + X.[8] + uint32 0x6FA87E4F) 6
            B <- A + LS(P4 A D C + B + X.[15] + uint32 0xFE2CE6E0) 10
            C <- B + LS(P4 B A D + C + X.[6] + uint32 0xA3014314) 15
            D <- C + LS(P4 C B A + D + X.[13] + uint32 0x4E0811A1) 21
            A <- D + LS(P4 D C B + A + X.[4] + uint32 0xF7537E82) 6
            B <- A + LS(P4 A D C + B + X.[11] + uint32 0xBD3AF235) 10
            C <- B + LS(P4 B A D + C + X.[2] + uint32 0x2AD7D2BB) 15
            D <- C + LS(P4 C B A + D + X.[9] + uint32 0xEB86D391) 21

            state.[0] <- state.[0] + A
            state.[3] <- state.[3] + B
            state.[2] <- state.[2] + C
            state.[1] <- state.[1] + D
