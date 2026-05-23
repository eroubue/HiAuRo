import { defineComponent, h } from 'vue';

export const ShapesSymbolO = defineComponent({
  name: 'ShapesSymbolO',
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
        h('path', {"d": "M4.9998 2C6.5462 2 7.79961 3.34315 7.79961 5C7.79961 6.65685 6.5462 8 4.9998 8C3.45349 7.99989 2.2 6.65678 2.2 5C2.2 3.34322 3.45349 2.00011 4.9998 2ZM4.9998 3.2002C4.19285 3.20031 3.40019 3.92665 3.40019 5C3.40019 6.07335 4.19285 6.79969 4.9998 6.7998C5.80682 6.7998 6.60039 6.07344 6.60039 5C6.60039 3.92656 5.80682 3.2002 4.9998 3.2002Z", "fillRule": "evenodd"})
      ]
    );
  }
});
