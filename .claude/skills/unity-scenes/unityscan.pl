#!/usr/bin/perl
# Lecture seule des fichiers YAML Unity (scenes, prefabs, assets). Ne modifie jamais rien.
#
#   perl unityscan.pl who       <guid>                    guid -> chemin de l'asset
#   perl unityscan.pl uses      <asset|nom|guid>          scenes / prefabs / assets qui le referencent
#   perl unityscan.pl overrides <scene> <prefab|nom|guid> overrides lisibles de chaque instance du prefab
#   perl unityscan.pl object    <fichier> <fileID>        nom du GameObject + type du composant
#   perl unityscan.pl texts     <scene|prefab|asset>      textes affiches (TMP, Text, TextMesh) + champs message/texte/label
#
# Chemins relatifs a la racine du repo ou absolus. Un "nom" = nom de fichier sans extension (UI, StartZone_8).
use strict;
use warnings;
use FindBin;
use File::Basename;
use File::Spec;

my $ROOT = File::Spec->rel2abs("$FindBin::Bin/../../..");
my $ASSETS = "$ROOT/Assets";
chdir $ROOT or die "racine du repo introuvable : $ROOT\n";

my %CLASS = (
    1 => 'GameObject', 4 => 'Transform', 20 => 'Camera', 23 => 'MeshRenderer', 33 => 'MeshFilter',
    54 => 'Rigidbody', 64 => 'MeshCollider', 65 => 'BoxCollider', 82 => 'AudioSource', 95 => 'Animator',
    96 => 'TrailRenderer', 108 => 'Light', 120 => 'LineRenderer', 135 => 'SphereCollider',
    136 => 'CapsuleCollider', 137 => 'SkinnedMeshRenderer', 198 => 'ParticleSystem',
    199 => 'ParticleSystemRenderer', 212 => 'SpriteRenderer', 222 => 'CanvasRenderer', 223 => 'Canvas',
    224 => 'RectTransform', 225 => 'CanvasGroup', 1001 => 'PrefabInstance',
);

# ============================================
# GUID -> CHEMIN (grep sur les .meta : Assets, puis Packages et PackageCache)
# ============================================

my %guidToPath;

sub guid_path {
    my ($guid) = @_;
    return undef unless defined $guid && $guid =~ /^[0-9a-f]{32}$/;
    return $guidToPath{$guid} if exists $guidToPath{$guid};
    my $found;
    for my $dir (qw(Assets Packages Library/PackageCache)) {
        next unless -d $dir;
        my $out = `grep -rl --include=*.meta "^guid: $guid" "$dir" 2>/dev/null`;
        if ($out =~ /^(.+?)\.meta\s*$/m) { $found = $1; last; }
    }
    $guidToPath{$guid} = $found;
    return $found;
}

sub rel {
    my ($p) = @_;
    $p =~ s/\\/\//g;
    (my $r = $ROOT) =~ s/\\/\//g;
    $p =~ s/^\Q$r\E\/?//;
    return $p;
}

sub abs_path {
    my ($p) = @_;
    return $p if File::Spec->file_name_is_absolute($p) && -e $p;
    return "$ROOT/$p" if -e "$ROOT/$p";
    return "$ASSETS/$p" if -e "$ASSETS/$p";
    return $p;
}

# Accepte un chemin, un guid ou un nom sans extension -> (chemin relatif, guid)
sub resolve_asset {
    my ($arg) = @_;
    if ($arg =~ /^[0-9a-f]{32}$/) {
        my $p = guid_path($arg);
        die "guid $arg introuvable\n" unless $p;
        return ($p, $arg);
    }
    my $path = abs_path($arg);
    unless (-e $path) {
        (my $safe = $arg) =~ s/"/\\"/g;
        my @found = grep { !/\.meta$/ } split /\n/, `find Assets -type f -name "$safe.*" 2>/dev/null`;
        die "aucun asset nomme '$arg'\n" unless @found;
        die "plusieurs assets nommes '$arg', preciser le chemin :\n  " . join("\n  ", map { rel($_) } @found) . "\n" if @found > 1;
        $path = $found[0];
    }
    open(my $fh, '<', "$path.meta") or die "pas de .meta pour $path\n";
    while (my $line = <$fh>) {
        return (rel($path), $1) if $line =~ /^guid:\s*([0-9a-f]{32})/;
    }
    die "pas de guid dans $path.meta\n";
}

# ============================================
# LECTURE D'UN FICHIER YAML UNITY -> blocs par fileID
# ============================================

my %fileCache;

sub load_file {
    my ($path) = @_;
    $path = abs_path($path);
    return $fileCache{$path} if $fileCache{$path};
    open(my $fh, '<', $path) or die "impossible d'ouvrir $path\n";
    my (%blocks, $cur);
    while (my $line = <$fh>) {
        $line =~ s/\r?\n$//;
        if ($line =~ /^--- !u!(\d+) &(-?\d+)( stripped)?/) {
            $cur = { type => $1, id => $2, stripped => defined $3, lines => [] };
            $blocks{$2} = $cur;
            next;
        }
        push @{$cur->{lines}}, $line if $cur;
    }
    close $fh;
    $fileCache{$path} = \%blocks;
    return \%blocks;
}

sub field {
    my ($block, $name) = @_;
    for (@{$block->{lines}}) {
        return $1 if /^  \Q$name\E:\s*(.*)$/;
    }
    return undef;
}

sub ref_of {
    my ($text) = @_;
    return () unless defined $text;
    my ($id) = $text =~ /fileID:\s*(-?\d+)/;
    my ($guid) = $text =~ /guid:\s*([0-9a-f]{32})/;
    return ($id, $guid);
}

sub script_name {
    my ($block) = @_;
    my $cls = field($block, 'm_EditorClassIdentifier');
    if (defined $cls && $cls =~ /::(\S+)$/) { return $1; }
    my (undef, $guid) = ref_of(field($block, 'm_Script'));
    if ($guid) {
        my $p = guid_path($guid);
        return basename($p, '.cs') if $p;
        return "script?$guid";
    }
    return 'MonoBehaviour';
}

sub type_name {
    my ($block) = @_;
    return script_name($block) if $block->{type} == 114;
    return $CLASS{$block->{type}} // "classe $block->{type}";
}

# Decrit un objet : "NomDuGO [Composant]". Suit les objets "stripped" vers leur prefab source.
sub describe {
    my ($path, $id, $depth) = @_;
    $depth //= 0;
    return "(rien)" if !defined $id || $id eq '0';
    return "(trop d'imbrication)" if $depth > 8;
    my $blocks = load_file($path);
    my $b = $blocks->{$id};
    unless ($b) {
        # Objet d'un prefab imbrique : fileID = (fileID de l'instance XOR fileID dans le prefab source) & 0x7FFF...
        for my $inst (grep { $_->{type} == 1001 && !$_->{stripped} } values %$blocks) {
            my (undef, $srcGuid) = ref_of(field($inst, 'm_SourcePrefab'));
            my $src = guid_path($srcGuid);
            next unless $src && $id =~ /^\d+$/;
            my $srcId = (($id + 0) ^ ($inst->{id} + 0)) & (~0 >> 1);
            return describe($src, $srcId, $depth + 1) . "  <imbrique : " . basename($src) . ">"
                if load_file($src)->{$srcId};
        }
        return "(fileID $id absent de " . rel(abs_path($path)) . " : override orphelin, objet supprime du prefab)";
    }

    if ($b->{stripped}) {
        my ($srcId, $srcGuid) = ref_of(field($b, 'm_CorrespondingSourceObject'));
        my $src = $srcGuid ? guid_path($srcGuid) : undef;
        return describe($src, $srcId, $depth + 1) if $src;
        return "(stripped, source introuvable)";
    }

    if ($b->{type} == 1) {
        return (field($b, 'm_Name') // '?') . " [GameObject]";
    }
    if ($b->{type} == 1001) {
        my (undef, $srcGuid) = ref_of(field($b, 'm_SourcePrefab'));
        return "(instance de " . (guid_path($srcGuid // '') // '?') . ")";
    }
    my ($goId) = ref_of(field($b, 'm_GameObject'));
    my $goName = '?';
    if (defined $goId && $goId ne '0') {
        ($goName = describe($path, $goId, $depth + 1)) =~ s/ \[GameObject\]$//;
    }
    return "$goName [" . type_name($b) . "]";
}

# ============================================
# COMMANDES
# ============================================

sub cmd_who {
    my ($guid) = @_;
    die "usage : who <guid>\n" unless $guid;
    my $p = guid_path($guid);
    print $p ? "$p\n" : "guid $guid introuvable (ni Assets, ni Packages, ni PackageCache)\n";
}

sub cmd_uses {
    my ($arg) = @_;
    die "usage : uses <asset|nom|guid>\n" unless $arg;
    my ($path, $guid) = resolve_asset($arg);
    print "$path (guid $guid)\n\n";
    my @hits;
    my $inc = join ' ', map { "--include=*.$_" } qw(unity prefab asset mat controller playable anim);
    for my $line (split /\n/, `grep -rc $inc "$guid" Assets 2>/dev/null`) {
        push @hits, [$1, $2] if $line =~ /^(.*):(\d+)$/ && $2 > 0;
    }
    if (!@hits) { print "aucune reference trouvee\n"; return; }
    for my $h (sort { $a->[0] cmp $b->[0] } @hits) {
        printf "  %-70s x%d\n", $h->[0], $h->[1];
    }
    printf "\n%d fichier(s)\n", scalar @hits;
}

sub cmd_object {
    my ($file, $id) = @_;
    die "usage : object <fichier> <fileID>\n" unless $file && defined $id;
    print describe($file, $id), "\n";
}

sub cmd_overrides {
    my ($scene, $prefabArg) = @_;
    die "usage : overrides <scene> <prefab|nom|guid>\n" unless $scene && $prefabArg;
    my ($prefabPath, $prefabGuid) = resolve_asset($prefabArg);
    my $sceneAbs = abs_path($scene);
    my $blocks = load_file($sceneAbs);

    my @instances = grep {
        $_->{type} == 1001 && !$_->{stripped}
            && ((ref_of(field($_, 'm_SourcePrefab')))[1] // '') eq $prefabGuid
    } values %$blocks;

    print rel($sceneAbs) . " : " . scalar(@instances) . " instance(s) de $prefabPath\n";
    return unless @instances;

    for my $inst (sort { $a->{id} <=> $b->{id} } @instances) {
        my @mods = parse_modifications($inst);
        my ($instName) = map { $_->{value} } grep { $_->{prop} eq 'm_Name' } @mods;
        print "\n=== Instance " . ($instName // basename($prefabPath, '.prefab')) . " (PrefabInstance $inst->{id})\n";

        my (%byTarget, @order);
        for my $m (@mods) {
            my $key = ($m->{guid} // '') . ":" . $m->{id};
            push @order, $key unless $byTarget{$key};
            push @{$byTarget{$key}}, $m;
        }
        for my $key (@order) {
            my $first = $byTarget{$key}[0];
            my $src = $first->{guid} ? guid_path($first->{guid}) : undef;
            my $who = $src ? describe($src, $first->{id}) : "(fileID $first->{id})";
            $who .= "  <prefab imbrique " . basename($src) . ">" if $src && $first->{guid} ne $prefabGuid;
            print "  $who\n";
            for my $m (@{$byTarget{$key}}) {
                my $val = $m->{value};
                my ($refId, $refGuid) = ref_of($m->{ref});
                if (defined $refId && $refId ne '0') {
                    my $refFile = $refGuid ? guid_path($refGuid) : rel($sceneAbs);
                    my $refDesc = $refGuid && $refFile && $refFile !~ /\.(unity|prefab)$/
                        ? $refFile
                        : ($refFile ? describe($refFile, $refId) : "guid $refGuid");
                    $val = "-> $refDesc";
                } elsif ($val eq '') {
                    $val = "(vide)";
                }
                print "      $m->{prop} = $val\n";
            }
        }

        for my $kind (qw(m_RemovedComponents m_RemovedGameObjects m_AddedGameObjects m_AddedComponents)) {
            my @items = list_section($inst, $kind);
            next unless @items;
            print "  $kind :\n";
            for my $it (@items) {
                my ($id, $guid) = ref_of($it);
                my $desc = $guid ? describe(guid_path($guid), $id) : describe($sceneAbs, $id);
                print "      $desc\n";
            }
        }
    }
}

sub parse_modifications {
    my ($inst) = @_;
    my (@mods, $cur, $inValue);
    for my $line (@{$inst->{lines}}) {
        if ($line =~ /^    - target:\s*(.*)$/) {
            my ($id, $guid) = ref_of($1);
            $cur = { id => $id, guid => $guid, prop => '', value => '', ref => '' };
            push @mods, $cur;
            $inValue = 0;
        } elsif ($cur && $line =~ /^      propertyPath:\s*(.*)$/) {
            $cur->{prop} = $1;
            $inValue = 0;
        } elsif ($cur && $line =~ /^      value:\s?(.*)$/) {
            $cur->{value} = $1;
            $inValue = 1;
        } elsif ($cur && $line =~ /^      objectReference:\s*(.*)$/) {
            $cur->{ref} = $1;
            $inValue = 0;
        } elsif ($cur && $inValue && $line =~ /^        (.*)$/) {
            $cur->{value} .= " $1";
        } elsif ($line =~ /^    m_(Removed|Added)/ || $line =~ /^  m_SourcePrefab/) {
            $cur = undef;
            $inValue = 0;
        }
    }
    for (@mods) { $_->{value} =~ s/^'(.*)'$/$1/; }
    return @mods;
}

sub list_section {
    my ($inst, $name) = @_;
    my (@items, $in);
    for my $line (@{$inst->{lines}}) {
        if ($line =~ /^    \Q$name\E:\s*(.*)$/) {
            last if $1 eq '[]';
            $in = 1;
            next;
        }
        if ($in) {
            if ($line =~ /^    - (.*)$/) { push @items, $1; }
            elsif ($line =~ /^      /) { next; }
            else { last; }
        }
    }
    return @items;
}

# ============================================
# TEXTES : ce qui s'affiche (TMP, Text, TextMesh) + champs de script qui ressemblent a un message
# ============================================

my $TEXT_FIELD = qr/^(m_text|m_Text)$/;
my $MSG_FIELD  = qr/(message|texte?|prompt|label|title|caption)/i;
my $MSG_SKIP   = qr/^m_|^text$|Font|Color|Size|Anim|Prefab|Object|Canvas|Ref|Component|Duration|Delay|Speed|Scale|Offset|Fade|Group|Rect|Transform|Image/;

sub yaml_unquote {
    my ($v) = @_;
    if ($v =~ /^'(.*)'$/s) {
        $v = $1; $v =~ s/''/'/g;
    } elsif ($v =~ /^"(.*)"$/s) {
        $v = $1;
        $v =~ s/\\u([0-9a-fA-F]{4})/chr(hex($1))/ge;
        $v =~ s/\\n/\n/g; $v =~ s/\\t/\t/g; $v =~ s/\\"/"/g; $v =~ s/\\\\/\\/g;
    }
    return $v;
}

# Valeur d'un champ de premier niveau (indentation 2), lignes de suite (indentation 4) comprises
sub read_scalar {
    my ($lines, $i) = @_;
    my ($txt) = $lines->[$i] =~ /^  [^:]+:\s?(.*)$/;
    return '' if $txt eq '';
    # chaine entre quotes : elle continue jusqu'a la quote fermante, quelle que soit l'indentation
    my $q = substr($txt, 0, 1);
    my $closed = sub {
        my ($t) = @_;
        return 1 unless $q eq "'" || $q eq '"';
        my $body = substr($t, 1);
        $body =~ s/''//g if $q eq "'";
        $body =~ s/\\.//g if $q eq '"';
        return index($body, $q) >= 0;
    };
    my $j = $i + 1;
    my $quoted = $q eq "'" || $q eq '"';
    while ($j < @$lines) {
        my $l = $lines->[$j];
        if ($quoted) { last if $closed->($txt); }
        else         { last if $l !~ /^    / && $l !~ /^\s*$/; }
        if ($l =~ /^\s*$/) { $txt .= "\n"; }
        else { (my $part = $l) =~ s/^\s+//; $txt .= ($txt =~ /\n$/ ? '' : ' ') . $part; }
        $j++;
    }
    $txt =~ s/\n(['"])$/$1/;   # dernier saut de ligne avant la quote fermante = fin de ligne du texte
    return yaml_unquote($txt);
}

sub show_text {
    my ($t) = @_;
    $t =~ s/\n/\\n/g;
    return $t eq '' ? '(vide)' : "\"$t\"";
}

sub cmd_texts {
    my ($file) = @_;
    die "usage : texts <scene|prefab|asset>\n" unless $file;
    my $abs = abs_path($file);
    $abs = abs_path((resolve_asset($file))[0]) unless -e $abs;
    my $blocks = load_file($abs);
    print rel($abs) . "\n";
    my $count = 0;

    for my $b (sort { $a->{id} <=> $b->{id} } values %$blocks) {
        next if $b->{stripped};
        next unless $b->{type} == 114 || $b->{type} == 102;
        my $lines = $b->{lines};
        my @found;
        for (my $i = 0; $i < @$lines; $i++) {
            my ($name, $rest) = $lines->[$i] =~ /^  ([A-Za-z_]\w*):\s?(.*)$/ or next;
            my $isText = $name =~ $TEXT_FIELD;
            my $isMsg  = !$isText && $name =~ $MSG_FIELD && $name !~ $MSG_SKIP;
            next unless $isText || $isMsg;
            if ($rest eq '' && $i + 1 < @$lines && $lines->[$i + 1] =~ /^  - (.*)$/) {
                # tableau de chaines (ex. deathMessages)
                my $k = $i + 1; my $n = 0;
                while ($k < @$lines && $lines->[$k] =~ /^  - (.*)$/) {
                    my $item = $1;
                    last if $item =~ /^\{|^\w+:/;
                    push @found, "$name\[$n\] = " . show_text(yaml_unquote($item));
                    $k++; $n++;
                }
                next;
            }
            next if $isMsg && ($rest eq '' || $rest =~ /^\{|^-?\d+(\.\d+)?$/);
            push @found, "$name = " . show_text(read_scalar($lines, $i));
        }
        next unless @found;
        my $who = describe($abs, $b->{id});
        my ($goId) = ref_of(field($b, 'm_GameObject'));
        my $go = defined $goId ? $blocks->{$goId} : undef;
        $who .= "  (GO inactif)" if $go && !$go->{stripped} && (field($go, 'm_IsActive') // 1) eq '0';
        print "  $who\n";
        print "      $_\n" for @found;
        $count += @found;
    }

    # Textes changes par override sur les instances de prefab
    for my $inst (sort { $a->{id} <=> $b->{id} } grep { $_->{type} == 1001 && !$_->{stripped} } values %$blocks) {
        my @mods = grep {
            (my $leaf = $_->{prop}) =~ s/\.Array\.data\[\d+\]$//;
            $leaf =~ s/^.*\.//;
            $leaf =~ $TEXT_FIELD
                || ($leaf =~ $MSG_FIELD && $leaf !~ $MSG_SKIP && $_->{value} ne '' && $_->{value} !~ /^-?\d+(\.\d+)?$/)
        } parse_modifications($inst);
        next unless @mods;
        my (undef, $srcGuid) = ref_of(field($inst, 'm_SourcePrefab'));
        my $src = guid_path($srcGuid);
        print "  [override sur " . ($src ? basename($src) : '?') . "]\n";
        for my $m (@mods) {
            my $mg = $m->{guid} ? guid_path($m->{guid}) : undef;
            my $who = $mg ? describe($mg, $m->{id}) : "(fileID $m->{id})";
            print "      $who : $m->{prop} = " . show_text(yaml_unquote($m->{value})) . "\n";
            $count++;
        }
    }
    print "\n$count texte(s)\n";
}

# ============================================
# MAIN
# ============================================

my $cmd = shift @ARGV // '';
my %dispatch = (who => \&cmd_who, uses => \&cmd_uses, object => \&cmd_object, overrides => \&cmd_overrides, texts => \&cmd_texts);
unless ($dispatch{$cmd}) {
    print "usage : perl unityscan.pl who|uses|overrides|object|texts ...\n";
    exit 1;
}
eval { $dispatch{$cmd}->(@ARGV); 1 } or do { print STDERR "erreur : $@"; exit 1; };
