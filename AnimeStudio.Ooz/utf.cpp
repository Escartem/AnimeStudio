#include "utf.h"

#include <vector>

#ifdef _WIN32
#include <Windows.h>
#else
#include <unicase.h>
#include <unistr.h>
#endif

namespace bun {
	namespace util {
		std::u16string lowercase(std::u16string const& s) {
			if (s.empty()) {
				return std::u16string(s);
			}
#ifdef _WIN32
			auto src = reinterpret_cast<LPCWSTR>(s.data());
			auto src_cch = static_cast<int>(s.size());
			auto dst_cch = LCMapStringEx(LOCALE_NAME_INVARIANT, LCMAP_LOWERCASE, src, src_cch, nullptr, 0, nullptr, nullptr, 0);
			std::vector<char16_t> dst(dst_cch);
			LCMapStringEx(LOCALE_NAME_INVARIANT, LCMAP_LOWERCASE, src, src_cch, reinterpret_cast<LPWSTR>(dst.data()), dst_cch,
				nullptr, nullptr, 0);
			return std::u16string(std::u16string(dst.data(), dst.data() + dst.size()));
#else
			// TODO(LV): implement

			auto src = reinterpret_cast<uint16_t const*>(s.data());
			size_t dst_cch = 0;
			u16_tolower(src, s.size(), nullptr, nullptr, nullptr, &dst_cch);
			std::vector<char16_t> dst(dst_cch);
			u16_tolower(src, s.size(), nullptr, nullptr, reinterpret_cast<uint16_t*>(dst.data()), &dst_cch);
			return std::u16string(std::u16string_view(dst.data(), dst.size()));
#endif
		}

		namespace {
			template <typename String> void convert_string(std::u16string const& src, String& dest) {
				using CharT = typename String::value_type;
#ifdef _WIN32
				LPCWCH src_p = reinterpret_cast<LPCWCH>(src.data());
				int src_n = static_cast<int>(src.size());
				int cch = WideCharToMultiByte(CP_UTF8, 0, src_p, src_n, nullptr, 0, nullptr, nullptr);
				std::vector<char> buf(cch);
				WideCharToMultiByte(CP_UTF8, 0, src_p, src_n, buf.data(), static_cast<int>(buf.size()), nullptr, nullptr);
				dest.assign(reinterpret_cast<CharT*>(buf.data()), buf.size());
#else
				size_t cch{};
				uint8_t* p = u16_to_u8(reinterpret_cast<uint16_t const*>(src.data()), src.size(), nullptr, &cch);
				dest.assign(reinterpret_cast<CharT*>(p), cch);
				free(p);
#endif
			}

			void convert_string(std::string const& src, std::u16string& dest) {
#ifdef _WIN32
				LPCCH src_p = reinterpret_cast<LPCCH>(src.data());
				int src_n = static_cast<int>(src.size());
				int cch = MultiByteToWideChar(CP_UTF8, 0, src_p, src_n, nullptr, 0);
				std::vector<wchar_t> buf(cch);
				MultiByteToWideChar(CP_UTF8, 0, src_p, src_n, buf.data(), static_cast<int>(buf.size()));
				dest.assign(reinterpret_cast<char16_t*>(buf.data()), buf.size());
#else
				size_t cch{};
				uint16_t* p = u8_to_u16(reinterpret_cast<uint8_t const*>(src.data()), src.size(), nullptr, &cch);
				dest.assign(reinterpret_cast<char16_t*>(p), cch);
				free(p);
#endif
			}
		} // namespace

		std::string to_string(std::u16string const& s) {
			std::string ret;
			convert_string(s, ret);
			return ret;
		}

		std::u16string to_u16string(std::string const& s) {
			std::u16string ret;
			convert_string(s, ret);
			return ret;
		}
	}
}
