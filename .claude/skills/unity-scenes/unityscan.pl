#!/usr/bin/perl
# Lecture seule des fichiers YAML Unity (scenes, prefabs, assets). Ne modifie jamais rien.
#
#   perl unityscan.pl who       <guid>                    guid -> chemin de l'asset
#   perl unityscan.pl uses      <asset|nom|guid>          scenes / prefabs / assets qui le referencent
#   perl unityscan.pl overrides <scene> <prefab|nom|guid> overrides lisibles de chaque instance du prefab
#   perl unityscan.pl object    <fichier> <fileID>        nom du GameObject + type du composant
#
# Chemins relatifs a la racine du repo ou absolus. Un "nom" = nom de fichier sans extension (UI, StartZone_8).
use strict;
use warnings;
use FindBin;
use File::Find;
use File::Basename;
use File::Spec;

my $ROOT = File::Spec->rel2abs("$FindBin::Bin/../../..");
my $ASSETS = "$ROOT/Assets";

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
        if ($out =~ /^(.+?).metas*$/m) { $found = $1; last; }
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
        my @found;
        find({ no_chdir => 1, wanted => sub {
            return if /\.meta$/;
            my ($n) = fileparse($_, qr/\.[^.]*$/);
            push @found, $_ if $n eq $arg && -f $_;
        }}, $ASSETS);
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
    return "(fileID $id introuvable dans " . rel(abs_path($path)) . " : objet d'un prefab imbrique ?)" unless $b;

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
    find({ no_chdir => 1, wanted => sub {
        return unless /\.(unity|prefab|asset|mat|controller|playable|anim)$/;
        open(my $fh, '<', $_) or return;
        my $n = 0;
        while (my $line = <$fh>) { $n++ if index($line, $guid) >= 0; }
        close $fh;
        push @hits, [rel($File::Find::name), $n] if $n;
    }}, $ASSETS);
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
                } elsif ($m->{ref} =~ /fileID:\s*0/ && $val eq '' && $m->{prop} !~ /m_text|Message|Text$/) {
                    $val = "(vide / aucune reference)";
                } elsif ($val eq '') {
                    $val = "''";
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
# MAIN
# ============================================

my $cmd = shift @ARGV // '';
my %dispatch = (who => \&cmd_who, uses => \&cmd_uses, object => \&cmd_object, overrides => \&cmd_overrides);
unless ($dispatch{$cmd}) {
    print "usage : perl unityscan.pl who|uses|overrides|object ...\n";
    exit 1;
}
eval { $dispatch{$cmd}->(@ARGV); 1 } or do { print STDERR "erreur : $@"; exit 1; };
