// Where the pack's surfaces sit among the Unity view's subviews.
//
// Every surface that lives as a subview of the Unity view - this pack's, and
// those of any other package that follows the same convention - marks itself
// with a tier, layer.name = @"riseon.layer.<tier>", and goes in directly
// under the first subview, counted from the bottom, whose tier is HIGHER
// than its own; with none, on top. A view with no mark counts as tier 0.
// Equal tiers therefore stack in the order they arrived, and no package has
// to know any other's views to land in the right place: the table is the
// whole contract.
//
//   rect ad             -1000   (this pack)
//   half-screen ad       2000   (this pack)
//
// The full table, with the tiers other packages use, is in the pack's
// README. A full-screen ad is never in it: it is a presented controller, and
// a presented controller draws above every subview of the one that presented
// it.
//
// Each package writes this helper for itself rather than calling another's,
// so none of them depends on the others to be present.

#ifndef RAN_LAYER_ORDER_H
#define RAN_LAYER_ORDER_H

#import <UIKit/UIKit.h>

static NSString *const kRANLayerNamePrefix = @"riseon.layer.";

static const NSInteger kRANRectAdLayerTier = -1000;
static const NSInteger kRANHalfScreenAdLayerTier = 2000;

static inline NSInteger RANLayerTierOf(UIView *view) {
    NSString *name = view.layer.name;
    if (![name hasPrefix:kRANLayerNamePrefix]) return 0;

    return [[name substringFromIndex:kRANLayerNamePrefix.length] integerValue];
}

static inline void RANInsertSubviewAtTier(
        UIView *host
      , UIView *view
      , NSInteger tier) {
    view.layer.name = [NSString stringWithFormat:@"%@%ld"
          , kRANLayerNamePrefix
          , (long)tier];
    for (UIView *sibling in host.subviews) {
        if (sibling == view) continue;
        if (RANLayerTierOf(sibling) <= tier) continue;

        [host insertSubview:view belowSubview:sibling];
        return;
    }
    [host addSubview:view];
}

#endif // RAN_LAYER_ORDER_H