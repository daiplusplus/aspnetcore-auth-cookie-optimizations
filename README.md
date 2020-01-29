# aspnetcore-auth-cookie-optimizations

## Security Warning!!!

This library may be very inappropriate for your application. Please read the "Security Notes" section below!

## What is this?

* For background, please read this thread: https://github.com/dotnet/aspnetcore/issues/10951
* This repo contains my code posted to that issue.
* In short:
  * ASP.NET Core's default authentication ticket (stored in ASP.NET Core's authentication cookie) uses an inefficient binary representation.
  * In my project using OIDC JWT `id_token` and `access_token`, the ASP.NET Core-generated authentication cookie was over 7KiB in size. With this code my cookie was shrunk down to 2.6KiB without losing any stored information.
  * This 7KiB size was a huge problem because it exceeded the 4096 byte cookie size limit present in many browsers. ASP.NET Core does try to use chunked cookies, but some browsers and other parts of HTTP infrastructure also place limits on the total size of all cookies used in a request or associated with a domain.
  * If you're just storing a few simple `Claim` values then you do not need this library.
  * If you're storing many `Claim` values (more than 5 or 6 or so) and/or are including or embedding JWT tokens (such as OAuth2 or OIDC's `id_token` or `access_token`) inside the ticket / cookie then you might find this library useful.
* Please read the security warning below!

## Security Notes

*PLEASE DO NOT USE THE (DEFLATE) COMPRESSION FEATURE OF THIS LIBRARY IN HIGH-SECURITY APPLICATIONS!*

Such as banking, healthcare, anything processing sensitive PII, and so on.
    
Why? 

* This library **optionally** uses Deflate compression to compress values in the authentication ticket and authentication cookie.
* The data is encrypted *after* the data is compressed.
* **Encrypting data after the same data is compressed MAY introduce a security vulnerability!**
    
  1. Authentication tickets may contain user-provided data (e.g. the  - and they can also contain data which is meant to be kept a secret from the end-user.
      * An example of user-provided data is a user-provided email-address for the email `Claim`.
      * An example of a secret that the user shouldn't see may be an API key or `access_token` for a third-party web-service that you're storing in the user's ticket to reduce memory consumption on your server or because your web-application is stateless.
    
  2. After the authentication ticket is serialized (but before it's compressed) bytes in user-provided data may match bytes in secret data located elsewhere in the serialized ticket.
  3. If compression system sees those matching bytes then will result in a smaller output compared to if the user's data did not match.
  4. An attacker can detect the size differences to quickly (and surprisingly quickly!) determine the secret value. This is a *chosen-plaintext information leakage* vulnerability.
    
* I understand that if your ticket will not contain any values that should be kept a secret (other than the ticket itself) from the user or anyone who can observe the cookie in-motion then there does not exist a security vulnerability _but only for your application_.
    * i.e. if you're only storing user-provided `Claim` values then you should be able to use this library without introducing security risks.
    
* You can also always use this library without using Deflate compression, so other optimizations will be applied.

For more information on actual real-world attacks like these, see [CRIME](https://en.wikipedia.org/wiki/CRIME) and [BREACH](https://en.wikipedia.org/wiki/BREACH).

* **Important note:** Because I am not a cryptographer, nor in any way an expert in computer security or information security, there may still be information disclosure risks when using this library.

## How does this library work?

This library offers a few options for improving authentication ticket and authentication cookie size:

* DEFLATE compression of the serialized authentication ticket before it's encrypted.
    * **DO NOT USE THIS FEATURE IF YOUR TICKET CONTAINS SECRETS!** (Read the above Security Warning for an explanation)
    * This shrunk my cookie from ASP.NET Core's original 7083 bytes down to 3193 bytes (i.e. shrunk to 45% of its original size).
    
* `Claim` names and types are interned.
    * i.e. well-known and common `Claim` names and types are represented by a 1-byte integer instead of their full string representations.
    * This is a huge saver when using `Claim` instances with non-`String` values (e.g. `Integer`) because otherwise ASP.NET Core writes the full SOAP-WS Claim name and SOAP claim data type name to the serialized ticket.
       * e.g. it will write `http://schemas.xmlsoap.org/ws/2009/09/identity/claims/actor` and ``
       * I'm surprised that in 2019 that ASP.NET Core continues to use old-fashioned SOAP-WS claim strings as through it's the mid-2000s in a clunky "Enterprise" system instead of much shorter JWT claim names.

* Preventing double-encoding of Base64 values, including JWTs (which are dot-separated Base64 strings).
    * If you're storing a Base64-esque JWT `access_token` and/or `id_token` directly inside your authentication ticket then you may be surprised (if not disappointed) to learn that ASP.NET Core will copy that string into the ticket as-is during serialization, and then Base64-encode the entire ticket *again* after encryption.
    * Base64 values are already 33% larger than their raw binary representation (so 100 bytes require 133 Base64 characters), so without any compression, then this will mean a JWT that's 100 bytes as a raw binary blob will be about 135 bytes when Base64-encoded (133 characters, plus 2 dots) - then that will be Base64-encoded again after encryption to then be 178 bytes.
    * This library will inspect all strings longer than a few bytes to see if they're Base64 strings (or using JWT's Base64-esque system) and if so then converts them back to raw binary data which is then only Base64-encoded once (after the cookie is encrypted).

