#include <poe/util/sha256.hpp>

#include <memory>

#ifdef _WIN32
#include <Windows.h>
#include <bcrypt.h>
#pragma comment(lib, "Bcrypt.lib")
#else
#include <sodium/crypto_hash_sha256.h>
#endif

namespace poe::util {
	std::string digest_to_string(sha256_digest const& digest) {
		char buf[128];
		char* p = buf;
		for (auto b : digest) {
			sprintf(p, "%02x", b);
			p += 2;
		}
		return buf;
	}

#ifdef _WIN32
	struct sha256_win32 : sha256 {
		sha256_win32() {
			BCryptOpenAlgorithmProvider(&alg_handle_, BCRYPT_SHA256_ALGORITHM, nullptr, BCRYPT_HASH_REUSABLE_FLAG);
			BCryptCreateHash(alg_handle_, &hash_handle_, nullptr, 0, nullptr, 0, BCRYPT_HASH_REUSABLE_FLAG);
		}

		~sha256_win32() {
			BCryptDestroyHash(hash_handle_);
			BCryptCloseAlgorithmProvider(alg_handle_, 0);
		}

		sha256_win32(sha256_win32&) = delete;
		sha256_win32 operator=(sha256_win32&) = delete;

		void reset() override {
			finish();
		}

		void feed(uint8_t const* data, size_t size) override {
			UCHAR* p = const_cast<UCHAR*>(reinterpret_cast<UCHAR const*>(data));
			ULONG n = static_cast<ULONG>(size);
			BCryptHashData(hash_handle_, p, n, 0);
		}

		sha256_digest finish() override {
			sha256_digest ret{};

			UCHAR* p = const_cast<UCHAR*>(reinterpret_cast<UCHAR const*>(ret.data()));
			ULONG n = static_cast<ULONG>(ret.size());
			BCryptFinishHash(hash_handle_, p, n, 0);
			return ret;
		}

	private:
		BCRYPT_ALG_HANDLE alg_handle_;
		BCRYPT_HASH_HANDLE hash_handle_;
	};
	using sha256_impl = sha256_win32;

#else
	struct sha256_linux : sha256 {
		sha256_linux() {
			reset();
		}

		sha256_linux(sha256_linux&) = delete;
		sha256_linux operator=(sha256_linux&) = delete;

		void reset() override {
			crypto_hash_sha256_init(&state_);
		}

		void feed(uint8_t const* data, size_t size) override {
			crypto_hash_sha256_update(&state_, reinterpret_cast<unsigned char const*>(data), size);
		}

		sha256_digest finish() override {
			sha256_digest ret{};
			crypto_hash_sha256_final(&state_, reinterpret_cast<unsigned char*>(ret.data()));
			reset();
			return ret;
		}

	private:
		crypto_hash_sha256_state state_;
	};
	using sha256_impl = sha256_linux;

#endif

	sha256_digest oneshot_sha256(uint8_t const* data, size_t size) {
		sha256_impl hasher;
		hasher.feed(data, size);
		return hasher.finish();
	}

	std::unique_ptr<sha256> incremental_sha256() { return std::make_unique<sha256_impl>(); }

} // namespace poe::util
