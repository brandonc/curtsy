# Explain #

> A hyperlinked, readable, c#-to-annotated-html documentation generator.

[See example output][pages]

Explain is a fork of [nocco][]* that does some lexical analysis in order to provide type hyperlinking for c#.

Nocco is simpler and yet can be run against more programming languages, comparatively speaking. It just doesn't hyperlink the source.

<small>* nocco is a port of [docco][]!</small>

### Caveats ###

This is a very early work in progress. Intellisense comments are ignored completely. Much hasn't been tested.

### To do ###

1. Type mapping is weak because namespace scope isn't taken into account.
2. Move core files out of console app so self-documentation has a better title
3. Hyperlinks within same file between sections
4. Test weirder code and code with syntax errors

[docco]: http://jashkenas.github.com/docco/
[nocco]: http://dontangg.github.com/nocco/
[pages]: http://brandoncroft.com/explain/