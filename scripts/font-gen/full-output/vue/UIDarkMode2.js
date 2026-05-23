import { defineComponent, h } from 'vue';

export const UIDarkMode2 = defineComponent({
  name: 'UIDarkMode2',
  props: {
    class: {
      type: String,
      default: ''
    }
  },
  setup(props, { attrs }) {
    return () => h(
      'svg',
      {
        viewBox: '0 0 20 20',
        
        class: `game-icons ${props.class}`,
        ...attrs
      },
      [
        h('path', {"d": "M5.27322 2.2002C5.6004 2.20024 5.6628 2.6453 5.45763 2.90016C5.18226 3.24224 5.01737 3.67675 5.01736 4.15039C5.01736 5.25496 5.9128 6.15039 7.01736 6.15039C7.35209 6.15035 7.66919 6.47952 7.46105 6.74168C6.94936 7.38617 6.16064 7.79969 5.27322 7.7998C3.72683 7.7998 2.47244 6.5464 2.47244 5C2.47259 3.45372 3.72691 2.2002 5.27322 2.2002Z", "fillRule": "evenodd"})
      ]
    );
  }
});
