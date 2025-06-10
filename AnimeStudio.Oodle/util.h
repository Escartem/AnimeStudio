#pragma once

#include <cstring>
#include <filesystem>
#include <string>
#include <vector>

std::string hex_dump(size_t width, void const* data, size_t size);

bool slurp_file(std::filesystem::path path, std::vector<uint8_t>& data);
bool dump_file(std::filesystem::path filename, void const* data, size_t size);

struct reader {
	reader(void const* p, size_t n) : reader(reinterpret_cast<uint8_t const*>(p), n) {}

	template <typename T>
	reader(T const* p, size_t n) : p_(reinterpret_cast<uint8_t const*>(p)), n_(n * sizeof(T)) {}

	template <typename T>
	bool read(T& t) {
		size_t const k = sizeof(T);
		if (n_ < k) {
			return false;
		}
		memcpy(&t, p_, k);
		p_ += k;
		n_ -= k;
		return true;
	}

	template <typename T>
	bool read(std::vector<T>& v) {
		size_t const k = v.size() * sizeof(T);
		if (n_ < k) {
			return false;
		}
		memcpy(v.data(), p_, k);
		p_ += k;
		n_ -= k;
		return true;
	}

	bool read(std::string& s) {
		auto* beg = reinterpret_cast<char const*>(p_);
		auto* end = reinterpret_cast<char const*>(memchr(p_, 0, n_));
		if (!end) {
			return false;
		}
		s.assign(beg, end);
		p_ += s.size() + 1;
		n_ -= s.size() + 1;
		return true;
	}

	uint8_t const* p_;
	size_t n_;
};
