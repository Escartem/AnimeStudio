#pragma once

#include <string>

namespace bun {
	namespace util {
		std::string lowercase(std::string const& s);
		std::u16string lowercase(std::u16string const& s);
		std::string to_string(std::u16string const& s);
		std::u16string to_u16string(std::string const& s);
	}
}