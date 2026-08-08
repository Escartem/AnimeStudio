#!/usr/bin/env python

import struct
import sys

from cffi import FFI
ffi = FFI()
ffi.cdef("""
	typedef uint8_t* OozMem;
	OozMem OozMemAlloc(size_t size);
	int64_t OozMemSize(OozMem mem);
	void OozMemFree(OozMem mem);
	
	int64_t OozDecompressBlock(uint8_t const* src_data, size_t src_size, uint8_t* dst_data, size_t dst_size);
	OozMem OozDecompressBlockAlloc(uint8_t const* src_data, size_t src_size, size_t dst_size);

	int64_t OozDecompressBundle(uint8_t const* src_data, size_t src_size, uint8_t* dst_data, size_t dst_size);
	OozMem OozDecompressBundleAlloc(uint8_t const* src_data, size_t src_size);
""")

ooz = ffi.dlopen("oozlib.dll")

cmd = sys.argv[1]
if cmd == 'block':
	filename = sys.argv[2]
	uncompressed_size = int(sys.argv[3])
	with open(filename, 'rb') as f:
		data = f.read()
		unpacked_data = ffi.new("uint8_t[]", uncompressed_size)
		unpacked_size = ooz.OozDecompressBlock(data, len(data), unpacked_data, uncompressed_size)
		if unpacked_size != uncompressed_size:
			printf("Could not decompress block", file=sys.stderr)
			exit(1)
		sys.stdout.buffer.write(ffi.buffer(unpacked_data))

elif cmd == 'bundle':
	filename = sys.argv[2]
	with open(filename, 'rb') as f:
		data = f.read()
		bundle_mem = ooz.OozDecompressBundleAlloc(data, len(data))
		if bundle_mem:
			size = ooz.OozMemSize(bundle_mem)
			sys.stdout.buffer.write(ffi.buffer(bundle_mem, size))
			ooz.OozMemFree(bundle_mem)
		else:
			print("Could not decompress bundle", file=sys.stderr)
			exit(1)